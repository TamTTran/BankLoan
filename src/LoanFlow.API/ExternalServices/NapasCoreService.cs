using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Production NAPAS Core Service — gọi NAPAS REST API thực.
/// Cấu hình qua NapasOptions (section "ExternalServices:Napas").
/// HttpClient có Polly retry pipeline (đăng ký trong Program.cs).
/// </summary>
public class NapasCoreService : INapasCoreService
{
    private readonly HttpClient _http;
    private readonly LoanFlowDbContext _db;
    private readonly NapasOptions _opts;
    private readonly ILogger<NapasCoreService> _logger;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public NapasCoreService(HttpClient http, LoanFlowDbContext db,
        IOptions<NapasOptions> opts, ILogger<NapasCoreService> logger)
    {
        _http   = http;
        _db     = db;
        _opts   = opts.Value;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(_opts.ApiUrl))
            _http.BaseAddress = new Uri(_opts.ApiUrl.TrimEnd('/') + "/");
    }

    // ── Transaction Query ────────────────────────────────────────────────────

    public async Task<Result<NapasTransactionDetail>> GetTransactionAsync(string napasTransactionId)
    {
        try
        {
            var res = await GetAsync<JsonElement>($"v1/transactions/{napasTransactionId}");
            if (!res.IsSuccess) return Result.Failure<NapasTransactionDetail>(res.Error!);
            return Result.Success(MapJsonToDetail(res.Value));
        }
        catch (Exception ex) { return Fail<NapasTransactionDetail>(ex); }
    }

    public async Task<Result<NapasTransactionDetail>> GetTransactionByReferenceAsync(string referenceId)
    {
        try
        {
            var res = await GetAsync<JsonElement>($"v1/transactions/by-reference/{referenceId}");
            if (!res.IsSuccess) return Result.Failure<NapasTransactionDetail>(res.Error!);
            return Result.Success(MapJsonToDetail(res.Value));
        }
        catch (Exception ex) { return Fail<NapasTransactionDetail>(ex); }
    }

    public async Task<Result<PagedResult<NapasTransactionDetail>>> SearchTransactionsAsync(NapasTransactionSearchRequest req)
    {
        try
        {
            var body = JsonSerializer.Serialize(req, _json);
            var res  = await PostAsync<JsonElement>("v1/transactions/search", body);
            if (!res.IsSuccess) return Result.Failure<PagedResult<NapasTransactionDetail>>(res.Error!);

            var items = res.Value.TryGetProperty("items", out var arr)
                ? arr.EnumerateArray().Select(MapJsonToDetail).ToList()
                : new List<NapasTransactionDetail>();
            var total = res.Value.TryGetProperty("totalCount", out var tc) ? tc.GetInt32() : items.Count;

            return Result.Success(new PagedResult<NapasTransactionDetail>
            {
                Items = items, TotalCount = total, Page = req.Page, PageSize = req.PageSize
            });
        }
        catch (Exception ex) { return Fail<PagedResult<NapasTransactionDetail>>(ex); }
    }

    // ── Merchant Info ────────────────────────────────────────────────────────

    public async Task<Result<NapasMerchantInfo>> GetMerchantInfoAsync()
    {
        try
        {
            var res = await GetAsync<NapasMerchantInfo>("v1/merchant");
            return res;
        }
        catch (Exception ex) { return Fail<NapasMerchantInfo>(ex); }
    }

    public async Task<Result<NapasMerchantSettings>> GetMerchantSettingsAsync()
    {
        try { return await GetAsync<NapasMerchantSettings>("v1/merchant/settings"); }
        catch (Exception ex) { return Fail<NapasMerchantSettings>(ex); }
    }

    // ── Reconciliation ───────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<NapasRecoFileInfo>>> ListRecoFilesAsync(DateOnly date)
    {
        try
        {
            var res = await GetAsync<IEnumerable<NapasRecoFileInfo>>($"v1/reconciliation/files?date={date:yyyy-MM-dd}");
            return res;
        }
        catch (Exception ex) { return Fail<IEnumerable<NapasRecoFileInfo>>(ex); }
    }

    public async Task<Result<NapasRecoFileContent>> DownloadRecoFileAsync(DateOnly date, string fileType)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"v1/reconciliation/files/{date:yyyy-MM-dd}?type={fileType}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return Result.Failure<NapasRecoFileContent>($"NAPAS reco download failed: HTTP {(int)resp.StatusCode}");

            var raw     = await resp.Content.ReadAsStringAsync();
            var records = ParseCsv(raw);
            return Result.Success(new NapasRecoFileContent { BusinessDate = date, FileType = fileType, RawCsv = raw, Records = records });
        }
        catch (Exception ex) { return Fail<NapasRecoFileContent>(ex); }
    }

    public async Task<Result<NapasRecoMatchResult>> MatchRecoFileAsync(DateOnly date, int requestingUserId)
    {
        // Delegate matching logic — identical to Mock (business logic, not NAPAS API call)
        var fileResult = await DownloadRecoFileAsync(date, "settlement");
        if (!fileResult.IsSuccess) return Result.Failure<NapasRecoMatchResult>(fileResult.Error!);

        var napasRecords  = fileResult.Value!.Records.ToList();
        var internals     = await _db.LoanDisbursements.AsNoTracking()
            .Where(d => d.DisbursementMethod == "BankTransfer" && DateOnly.FromDateTime(d.CreatedAt) == date)
            .ToListAsync();

        var internalById  = internals.Where(d => d.NapasReferenceId != null).ToDictionary(d => d.NapasReferenceId!);
        var napasById     = napasRecords.ToDictionary(r => r.ReferenceId);

        var unmatchedNapas    = napasRecords.Where(r => !internalById.ContainsKey(r.ReferenceId)).ToList();
        var unmatchedInternal = internals.Where(d => d.NapasReferenceId == null || !napasById.ContainsKey(d.NapasReferenceId))
            .Select(d => new UnmatchedInternal { DisbursementId = d.Id, NapasReferenceId = d.NapasReferenceId ?? $"MISSING-{d.Id}", Amount = d.DisbursedAmount, DisbursementStatus = d.DisbursementStatus, CreatedAt = d.CreatedAt }).ToList();
        var mismatches = napasRecords.Where(r => internalById.ContainsKey(r.ReferenceId) && Math.Abs(r.Amount - internalById[r.ReferenceId].DisbursedAmount) > 0.01m)
            .Select(r => new RecoAmountMismatch { ReferenceId = r.ReferenceId, NapasAmount = r.Amount, InternalAmount = internalById[r.ReferenceId].DisbursedAmount, Difference = r.Amount - internalById[r.ReferenceId].DisbursedAmount }).ToList();

        int created = 0;
        foreach (var u in unmatchedNapas)
        {
            _db.ReconciliationExceptions.Add(new ReconciliationException
            {
                BusinessDate = date, ExceptionType = ReconciliationExceptionType.DisbursementMismatch,
                ExpectedAmount = u.Amount, ActualAmount = 0, Difference = u.Amount,
                Description = $"NAPAS {u.TransactionId} (Ref:{u.ReferenceId}) not in internal DB.", Status = ReconciliationStatus.Open
            });
            created++;
        }
        if (created > 0) await _db.SaveChangesAsync();

        return Result.Success(new NapasRecoMatchResult
        {
            BusinessDate = date, TotalNapasRecords = napasRecords.Count, TotalInternalRecords = internals.Count,
            MatchedCount = napasRecords.Count - unmatchedNapas.Count - mismatches.Count, CreatedExceptions = created,
            TotalNapasAmount = napasRecords.Sum(r => r.Amount), TotalInternalAmount = internals.Sum(d => d.DisbursedAmount),
            AmountDifference = napasRecords.Sum(r => r.Amount) - internals.Sum(d => d.DisbursedAmount),
            UnmatchedNapasRecords = unmatchedNapas, UnmatchedInternalRecords = unmatchedInternal, AmountMismatches = mismatches
        });
    }

    // ── QR Code (offline EMV QRCPS — identical to Mock) ─────────────────────

    public async Task<Result<NapasQrResponse>> GenerateQrAsync(NapasQrRequest req)
    {
        // QR generation is fully offline — delegate to mock logic (no API call)
        var mock = new MockNapasCoreService(_db, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockNapasCoreService>.Instance);
        return await mock.GenerateQrAsync(req);
    }

    public async Task<Result<NapasQrDecoded>> DecodeQrAsync(string qrString)
    {
        var mock = new MockNapasCoreService(_db, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockNapasCoreService>.Instance);
        return await mock.DecodeQrAsync(qrString);
    }

    public async Task<Result<NapasQrResponse>> GetOrCreateLoanQrAsync(int loanId)
    {
        var mock = new MockNapasCoreService(_db, Microsoft.Extensions.Logging.Abstractions.NullLogger<MockNapasCoreService>.Instance);
        return await mock.GetOrCreateLoanQrAsync(loanId);
    }

    // ── HTTP helpers ─────────────────────────────────────────────────────────

    private async Task<Result<T>> GetAsync<T>(string path)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return Result.Failure<T>($"NAPAS {path} HTTP {(int)resp.StatusCode}: {body}");
        return Result.Success(JsonSerializer.Deserialize<T>(body, _json)!);
    }

    private async Task<Result<T>> PostAsync<T>(string path, string jsonBody)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return Result.Failure<T>($"NAPAS {path} HTTP {(int)resp.StatusCode}: {body}");
        return Result.Success(JsonSerializer.Deserialize<T>(body, _json)!);
    }

    private static Result<T> Fail<T>(Exception ex) =>
        Result.Failure<T>($"NAPAS Core error: {ex.Message}");

    private static NapasTransactionDetail MapJsonToDetail(JsonElement j) => new()
    {
        TransactionId   = j.TryGetProperty("transactionId",   out var t)  ? t.GetString()  ?? "" : "",
        ReferenceId     = j.TryGetProperty("referenceId",     out var r)  ? r.GetString()  ?? "" : "",
        Status          = j.TryGetProperty("status",          out var s)  ? s.GetString()  ?? "" : "",
        Amount          = j.TryGetProperty("amount",          out var a)  ? a.GetDecimal()     : 0,
        BankCode        = j.TryGetProperty("bankCode",        out var b)  ? b.GetString()  ?? "" : "",
        AccountNumber   = j.TryGetProperty("accountNumber",   out var ac) ? ac.GetString() ?? "" : "",
        TransactionType = j.TryGetProperty("transactionType", out var tt) ? tt.GetString() ?? "IBFT" : "IBFT",
        ProcessedAt     = j.TryGetProperty("processedAt",     out var pa) ? pa.GetDateTime()   : DateTime.UtcNow,
        ErrorCode       = j.TryGetProperty("errorCode",       out var ec) ? ec.GetString()     : null,
        ErrorMessage    = j.TryGetProperty("errorMessage",    out var em) ? em.GetString()     : null
    };

    private static IEnumerable<NapasRecoRecord> ParseCsv(string raw)
    {
        var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1); // skip header
        foreach (var line in lines)
        {
            var cols = line.Split(',');
            if (cols.Length < 7) continue;
            yield return new NapasRecoRecord
            {
                TransactionId = cols[0].Trim(), ReferenceId = cols[1].Trim(),
                Amount        = decimal.TryParse(cols[2].Trim(), out var amt) ? amt : 0,
                Status        = cols[3].Trim(), BankCode = cols[4].Trim(), AccountNumber = cols[5].Trim(),
                ProcessedAt   = DateTime.TryParse(cols[6].Trim(), out var dt) ? dt : DateTime.UtcNow,
                FeeAmount     = cols.Length > 7 && decimal.TryParse(cols[7].Trim(), out var fee) ? fee : 0
            };
        }
    }
}
