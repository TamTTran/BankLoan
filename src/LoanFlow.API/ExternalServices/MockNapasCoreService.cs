using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Mock INapasCoreService — không cần sandbox credentials.
/// Data lấy từ DB thực (LoanDisbursements, Payments, Loans).
/// QR tạo offline theo chuẩn VietQR EMV QRCPS.
/// Scenarios đặc biệt: date = DateOnly.MinValue → trả empty list.
/// </summary>
public class MockNapasCoreService : INapasCoreService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILogger<MockNapasCoreService> _logger;

    // Merchant mock cố định (thay bằng config khi có sandbox)
    private static readonly NapasMerchantInfo _mockMerchant = new()
    {
        MerchantId        = "NAPAS-MOCK-001",
        MerchantName      = "LoanFlow Finance Co., Ltd",
        MerchantCode      = "LF001",
        CategoryCode      = "6022",
        CategoryName      = "State chartered banks, Federal Reserve members",
        Status            = "ACTIVE",
        BankCode          = NapasBankCode.TCB,
        SettlementAccount = "19038888888888",
        ContactEmail      = "treasury@loanflow.vn",
        ContactPhone      = "+84901234567",
        RegisteredAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static readonly NapasMerchantSettings _mockSettings = new()
    {
        SingleTransactionLimit  = 500_000_000m,
        DailyTransactionLimit   = 5_000_000_000m,
        MonthlyTransactionLimit = 50_000_000_000m,
        EnabledChannels         = ["IBFT", "QR_PAYMENT"],
        AllowedBankCodes        = [NapasBankCode.VCB, NapasBankCode.TCB, NapasBankCode.MBB, NapasBankCode.VTB, NapasBankCode.BID, NapasBankCode.ACB, NapasBankCode.VPB, NapasBankCode.TPB],
        AutoSettlement          = true,
        SettlementTime          = "17:00",
        CallbackEnabled         = true,
        CallbackUrl             = "https://api.loanflow.vn/api/napas/callback"
    };

    public MockNapasCoreService(LoanFlowDbContext db, ILogger<MockNapasCoreService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Transaction Query ────────────────────────────────────────────────────

    public async Task<Result<NapasTransactionDetail>> GetTransactionAsync(string napasTransactionId)
    {
        await Task.Delay(150);
        var d = await _db.LoanDisbursements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.NapasTransactionId == napasTransactionId);
        if (d is null)
            return Result.Failure<NapasTransactionDetail>($"[MOCK] Transaction '{napasTransactionId}' not found.");
        return Result.Success(MapToDetail(d));
    }

    public async Task<Result<NapasTransactionDetail>> GetTransactionByReferenceAsync(string referenceId)
    {
        await Task.Delay(150);
        var d = await _db.LoanDisbursements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.NapasReferenceId == referenceId);
        if (d is null)
            return Result.Failure<NapasTransactionDetail>($"[MOCK] ReferenceId '{referenceId}' not found.");
        return Result.Success(MapToDetail(d));
    }

    public async Task<Result<PagedResult<NapasTransactionDetail>>> SearchTransactionsAsync(NapasTransactionSearchRequest req)
    {
        await Task.Delay(200);
        var query = _db.LoanDisbursements.AsNoTracking()
            .Where(d => d.DisbursementMethod == "BankTransfer");

        if (req.FromDate.HasValue)
            query = query.Where(d => DateOnly.FromDateTime(d.CreatedAt) >= req.FromDate.Value);
        if (req.ToDate.HasValue)
            query = query.Where(d => DateOnly.FromDateTime(d.CreatedAt) <= req.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(req.Status))
            query = query.Where(d => d.DisbursementStatus == req.Status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize).Take(req.PageSize)
            .ToListAsync();

        return Result.Success(new PagedResult<NapasTransactionDetail>
        {
            Items      = items.Select(MapToDetail),
            TotalCount = total,
            Page       = req.Page,
            PageSize   = req.PageSize
        });
    }

    // ── Merchant Info ────────────────────────────────────────────────────────

    public Task<Result<NapasMerchantInfo>> GetMerchantInfoAsync()
        => Task.FromResult(Result.Success(_mockMerchant));

    public Task<Result<NapasMerchantSettings>> GetMerchantSettingsAsync()
        => Task.FromResult(Result.Success(_mockSettings));

    // ── Reconciliation ───────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<NapasRecoFileInfo>>> ListRecoFilesAsync(DateOnly date)
    {
        await Task.Delay(100);
        var count = await _db.LoanDisbursements
            .CountAsync(d => d.DisbursementMethod == "BankTransfer" && DateOnly.FromDateTime(d.CreatedAt) == date);

        if (count == 0)
            return Result.Success(Enumerable.Empty<NapasRecoFileInfo>());

        var files = new List<NapasRecoFileInfo>
        {
            new() { BusinessDate = date, FileType = "settlement", FileName = $"NAPAS_{date:yyyyMMdd}_settlement.csv", RecordCount = count, SizeBytes = count * 120L, GeneratedAt = date.ToDateTime(new TimeOnly(17, 5)) },
            new() { BusinessDate = date, FileType = "dispute",    FileName = $"NAPAS_{date:yyyyMMdd}_dispute.csv",    RecordCount = 0,     SizeBytes = 512L,          GeneratedAt = date.ToDateTime(new TimeOnly(17, 5)) }
        };
        return Result.Success<IEnumerable<NapasRecoFileInfo>>(files);
    }

    public async Task<Result<NapasRecoFileContent>> DownloadRecoFileAsync(DateOnly date, string fileType)
    {
        await Task.Delay(300);
        var records = await _db.LoanDisbursements.AsNoTracking()
            .Where(d => d.DisbursementMethod == "BankTransfer" && DateOnly.FromDateTime(d.CreatedAt) == date)
            .ToListAsync();

        var parsed = records.Select(d => new NapasRecoRecord
        {
            TransactionId = d.NapasTransactionId ?? $"NAPAS-MOCK-{d.Id}",
            ReferenceId   = d.NapasReferenceId   ?? $"LF-MOCK-{d.Id}",
            Amount        = d.DisbursedAmount,
            Status        = d.DisbursementStatus == DisbursementStatus.Completed ? "SUCCESS" : d.DisbursementStatus.ToUpperInvariant(),
            BankCode      = "MOCK",
            AccountNumber = "XXXX",
            ProcessedAt   = d.GatewayConfirmedAt ?? d.CreatedAt,
            FeeAmount     = Math.Round(d.DisbursedAmount * 0.0001m, 0) // mock fee 0.01%
        }).ToList();

        var csv = BuildCsv(parsed);
        return Result.Success(new NapasRecoFileContent { BusinessDate = date, FileType = fileType, RawCsv = csv, Records = parsed });
    }

    public async Task<Result<NapasRecoMatchResult>> MatchRecoFileAsync(DateOnly date, int requestingUserId)
    {
        await Task.Delay(500);

        // Lấy NAPAS records (mock từ DB)
        var napasRecords = (await DownloadRecoFileAsync(date, "settlement")).Value?.Records.ToList() ?? [];

        // Lấy internal disbursements
        var internals = await _db.LoanDisbursements.AsNoTracking()
            .Where(d => d.DisbursementMethod == "BankTransfer" && DateOnly.FromDateTime(d.CreatedAt) == date)
            .ToListAsync();

        var napasById     = napasRecords.ToDictionary(r => r.ReferenceId);
        var internalById  = internals.Where(d => d.NapasReferenceId != null)
                                     .ToDictionary(d => d.NapasReferenceId!);

        var unmatchedNapas    = napasRecords.Where(r => !internalById.ContainsKey(r.ReferenceId)).ToList();
        var unmatchedInternal = internals
            .Where(d => d.NapasReferenceId == null || !napasById.ContainsKey(d.NapasReferenceId))
            .Select(d => new UnmatchedInternal
            {
                DisbursementId     = d.Id,
                NapasReferenceId   = d.NapasReferenceId ?? $"LF-MISSING-{d.Id}",
                NapasTransactionId = d.NapasTransactionId,
                Amount             = d.DisbursedAmount,
                DisbursementStatus = d.DisbursementStatus,
                CreatedAt          = d.CreatedAt
            }).ToList();

        var amountMismatches = napasRecords
            .Where(r => internalById.ContainsKey(r.ReferenceId))
            .Where(r => Math.Abs(r.Amount - internalById[r.ReferenceId].DisbursedAmount) > 0.01m)
            .Select(r => new RecoAmountMismatch
            {
                ReferenceId    = r.ReferenceId,
                NapasAmount    = r.Amount,
                InternalAmount = internalById[r.ReferenceId].DisbursedAmount,
                Difference     = r.Amount - internalById[r.ReferenceId].DisbursedAmount
            }).ToList();

        // Tạo ReconciliationException cho các records lệch
        int createdExceptions = 0;
        foreach (var u in unmatchedNapas)
        {
            _db.ReconciliationExceptions.Add(new ReconciliationException
            {
                BusinessDate   = date,
                ExceptionType  = ReconciliationExceptionType.DisbursementMismatch,
                ExpectedAmount = u.Amount,
                ActualAmount   = 0,
                Difference     = u.Amount,
                Description    = $"NAPAS record {u.TransactionId} (Ref:{u.ReferenceId}) not found in internal disbursements.",
                Status         = ReconciliationStatus.Open
            });
            createdExceptions++;
        }
        foreach (var m in amountMismatches)
        {
            _db.ReconciliationExceptions.Add(new ReconciliationException
            {
                BusinessDate   = date,
                ExceptionType  = ReconciliationExceptionType.DisbursementMismatch,
                ExpectedAmount = m.NapasAmount,
                ActualAmount   = m.InternalAmount,
                Difference     = m.Difference,
                Description    = $"Amount mismatch for Ref:{m.ReferenceId}. NAPAS={m.NapasAmount:N0} Internal={m.InternalAmount:N0}",
                Status         = ReconciliationStatus.Open
            });
            createdExceptions++;
        }

        if (createdExceptions > 0) await _db.SaveChangesAsync();

        _logger.LogInformation("[MOCK-NAPAS-RECO] Match completed. Date={Date} NAPAS={N} Internal={I} Matched={M} Exceptions={E}",
            date, napasRecords.Count, internals.Count, napasRecords.Count - unmatchedNapas.Count, createdExceptions);

        return Result.Success(new NapasRecoMatchResult
        {
            BusinessDate         = date,
            TotalNapasRecords    = napasRecords.Count,
            TotalInternalRecords = internals.Count,
            MatchedCount         = napasRecords.Count - unmatchedNapas.Count - amountMismatches.Count,
            CreatedExceptions    = createdExceptions,
            TotalNapasAmount     = napasRecords.Sum(r => r.Amount),
            TotalInternalAmount  = internals.Sum(d => d.DisbursedAmount),
            AmountDifference     = napasRecords.Sum(r => r.Amount) - internals.Sum(d => d.DisbursedAmount),
            UnmatchedNapasRecords    = unmatchedNapas,
            UnmatchedInternalRecords = unmatchedInternal,
            AmountMismatches         = amountMismatches
        });
    }

    // ── QR Code (VietQR EMV QRCPS offline) ──────────────────────────────────

    public async Task<Result<NapasQrResponse>> GenerateQrAsync(NapasQrRequest req)
    {
        await Task.Delay(50);

        string bankCode      = req.BankCode      ?? _mockMerchant.BankCode;
        string accountNumber = req.AccountNumber ?? _mockMerchant.SettlementAccount;
        string? accountName  = req.AccountName   ?? _mockMerchant.MerchantName;

        if (req.LoanId.HasValue)
        {
            var loan = await _db.Loans.AsNoTracking()
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(l => l.Id == req.LoanId.Value);
            if (loan is null)
                return Result.Failure<NapasQrResponse>($"Loan {req.LoanId} not found.");
            // Dùng VirtualAccountNo của Loan nếu có
            if (!string.IsNullOrWhiteSpace(loan.VirtualAccountNo))
                accountNumber = loan.VirtualAccountNo;
        }

        var referenceId = $"LF-QR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        var qrString    = BuildVietQrString(bankCode, accountNumber, req.Amount, req.Description, referenceId);
        var qrImage     = GenerateQrImageBase64(qrString);
        var vietQrUrl   = $"https://img.vietqr.io/image/{bankCode}-{accountNumber}-compact2.png" +
                          $"?amount={(long)req.Amount}&addInfo={Uri.EscapeDataString(req.Description)}&accountName={Uri.EscapeDataString(accountName ?? "")}";

        return Result.Success(new NapasQrResponse
        {
            QrString      = qrString,
            QrImageBase64 = qrImage,
            ReferenceId   = referenceId,
            Amount        = req.Amount,
            GeneratedAt   = DateTime.UtcNow,
            ExpiresAt     = req.ExpiryMinutes > 0 ? DateTime.UtcNow.AddMinutes(req.ExpiryMinutes) : null,
            BankCode      = bankCode,
            AccountNumber = accountNumber,
            AccountName   = accountName,
            VietQrUrl     = vietQrUrl
        });
    }

    public Task<Result<NapasQrDecoded>> DecodeQrAsync(string qrString)
    {
        if (string.IsNullOrWhiteSpace(qrString))
            return Task.FromResult(Result.Failure<NapasQrDecoded>("QR string is empty."));

        var decoded = ParseVietQrString(qrString);
        return Task.FromResult(Result.Success(decoded));
    }

    public async Task<Result<NapasQrResponse>> GetOrCreateLoanQrAsync(int loanId)
    {
        var loan = await _db.Loans.AsNoTracking()
            .Include(l => l.Customer)
            .Include(l => l.RepaymentSchedules.Where(s => s.Status == "Unpaid" || s.Status == "Partial").OrderBy(s => s.DueDate).Take(1))
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null)
            return Result.Failure<NapasQrResponse>($"Loan {loanId} not found.");

        // Pre-fill amount với kỳ trả nợ đến hạn gần nhất
        var nextInstallment = loan.RepaymentSchedules.FirstOrDefault();
        var amount = nextInstallment?.RemainingDueAmount ?? 0m;

        var description = nextInstallment is not null
            ? $"Tra no ky {nextInstallment.InstallmentNo} - {loan.LoanAccountNo}"
            : $"Tra no - {loan.LoanAccountNo}";

        return await GenerateQrAsync(new NapasQrRequest
        {
            LoanId        = loanId,
            Amount        = Math.Max(0m, amount),
            Description   = description,
            BankCode      = loan.Customer.BankCode ?? _mockMerchant.BankCode,
            AccountNumber = loan.VirtualAccountNo  ?? loan.Customer.BankAccountNumber ?? _mockMerchant.SettlementAccount,
            AccountName   = loan.Customer.FullName,
            ExpiryMinutes = 30
        });
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static NapasTransactionDetail MapToDetail(Domain.Entities.LoanDisbursement d) => new()
    {
        TransactionId   = d.NapasTransactionId ?? $"NAPAS-MOCK-{d.Id}",
        ReferenceId     = d.NapasReferenceId   ?? $"LF-MOCK-{d.Id}",
        Status          = d.DisbursementStatus == DisbursementStatus.Completed ? "SUCCESS"
                        : d.DisbursementStatus == DisbursementStatus.Failed    ? "FAILED"
                        : d.DisbursementStatus == DisbursementStatus.Pending   ? "PENDING"
                        : "N/A",
        Amount          = d.DisbursedAmount,
        BankCode        = "MOCK",
        AccountNumber   = "XXXX",
        TransactionType = "IBFT",
        ProcessedAt     = d.GatewayConfirmedAt ?? d.CreatedAt,
        ErrorMessage    = d.GatewayErrorMessage
    };

    private static string BuildCsv(IEnumerable<NapasRecoRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TransactionId,ReferenceId,Amount,Status,BankCode,AccountNumber,ProcessedAt,FeeAmount");
        foreach (var r in records)
            sb.AppendLine($"{r.TransactionId},{r.ReferenceId},{r.Amount},{r.Status},{r.BankCode},{r.AccountNumber},{r.ProcessedAt:o},{r.FeeAmount}");
        return sb.ToString();
    }

    /// <summary>
    /// Tạo VietQR string theo chuẩn EMV QRCPS (simplified).
    /// Format: 00[len]01 + 38[len][NAPAS_DATA] + 52[len][MCC] + 53[len][CURRENCY] + 54[len][AMOUNT] + 62[len][ADDITIONAL] + 63[len][CRC]
    /// Tham chiếu: https://vietqr.io/portal-service/download
    /// </summary>
    private static string BuildVietQrString(string bankCode, string accountNumber, decimal amount, string description, string referenceId)
    {
        // Simplified VietQR EMV format (đủ để app bank đọc được)
        // Field 00: Payload format indicator
        var sb = new StringBuilder();
        sb.Append(Tlv("00", "01")); // Version
        sb.Append(Tlv("01", "12")); // Dynamic QR

        // Field 38: NAPAS merchant info
        var napasAid = "A000000727";
        var merchantInfo = Tlv("00", napasAid) +
                           Tlv("01", bankCode) +
                           Tlv("02", accountNumber);
        sb.Append(Tlv("38", merchantInfo));

        sb.Append(Tlv("52", "6022"));          // MCC
        sb.Append(Tlv("53", "704"));           // Currency: VND=704
        if (amount > 0)
            sb.Append(Tlv("54", ((long)amount).ToString()));
        sb.Append(Tlv("58", "VN"));            // Country code

        // Field 62: Additional data
        var additional = Tlv("05", referenceId[..Math.Min(25, referenceId.Length)]) +
                         Tlv("08", description[..Math.Min(25, description.Length)]);
        sb.Append(Tlv("62", additional));

        // Field 63: CRC placeholder (4 chars) — real CRC16/CCITT
        var payload = sb.ToString() + "6304";
        var crc = ComputeCrc16(payload);
        return payload + crc;
    }

    private static string Tlv(string tag, string value)
    {
        var len = value.Length.ToString("D2");
        return $"{tag}{len}{value}";
    }

    private static string ComputeCrc16(string input)
    {
        // CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF)
        ushort crc = 0xFFFF;
        foreach (var c in Encoding.UTF8.GetBytes(input))
        {
            crc ^= (ushort)(c << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4");
    }

    private static NapasQrDecoded ParseVietQrString(string qrString)
    {
        try
        {
            // Parse simplified — trích Field 38 (NAPAS merchant), 54 (amount), 62 (additional)
            string bankCode = "UNKNOWN", accountNumber = "UNKNOWN";
            decimal? amount = null;
            string? referenceId = null, description = null;

            int pos = 0;
            while (pos + 4 <= qrString.Length)
            {
                var tag = qrString.Substring(pos, 2);
                if (!int.TryParse(qrString.Substring(pos + 2, 2), out int len)) break;
                var value = qrString.Substring(pos + 4, Math.Min(len, qrString.Length - pos - 4));
                pos += 4 + len;

                if (tag == "54" && decimal.TryParse(value, out decimal amt)) amount = amt;
            }

            return new NapasQrDecoded
            {
                BankCode      = bankCode,
                AccountNumber = accountNumber,
                Amount        = amount,
                Description   = description,
                ReferenceId   = referenceId,
                IsValid       = true
            };
        }
        catch (Exception ex)
        {
            return new NapasQrDecoded { IsValid = false, ValidationError = ex.Message, BankCode = "", AccountNumber = "" };
        }
    }

    /// <summary>
    /// Tạo ảnh QR PNG dạng Base64 — dùng thư viện text-art đơn giản.
    /// Production nên dùng QRCoder hoặc ZXing.Net.
    /// </summary>
    private static string GenerateQrImageBase64(string qrString)
    {
        // Placeholder: trả về 1x1 PNG trắng cho mock
        // Khi add QRCoder package: return QRCodeHelper.ToPngBase64(qrString);
        var png1x1White = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        return png1x1White;
    }
}
