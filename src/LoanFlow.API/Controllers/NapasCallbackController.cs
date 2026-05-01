using LoanFlow.API.ExternalServices;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace LoanFlow.API.Controllers;

/// <summary>
/// Nhận callback async từ NAPAS sau khi xử lý lệnh chuyển tiền giải ngân.
///
/// Endpoint: POST /api/napas/callback
///
/// Security:
///   - HMAC-SHA256 signature verify qua header X-Napas-Signature
///   - Idempotent — xử lý trùng ReferenceId được bỏ qua
///   - IP whitelisting nên được thêm ở tầng Nginx trong production
///
/// Test với Mock (bankCode=ASYNC):
///   1. Disbursement với BankCode=ASYNC → DisbursementStatus=Pending
///   2. POST /api/napas/callback với payload dưới đây để confirm:
///      { "referenceId": "LF-...", "napasTransactionId": "NAPAS-001", "status": "SUCCESS", "processedAt": "..." }
///   3. DisbursementStatus → Completed
/// </summary>
[ApiController]
[Route("api/napas")]
public class NapasCallbackController : ControllerBase
{
    private readonly IPaymentGatewayService _gatewayService;
    private readonly LoanFlowDbContext _db;
    private readonly NapasOptions _options;
    private readonly ILogger<NapasCallbackController> _logger;

    public NapasCallbackController(
        IPaymentGatewayService gatewayService,
        LoanFlowDbContext db,
        IOptions<NapasOptions> options,
        ILogger<NapasCallbackController> logger)
    {
        _gatewayService = gatewayService;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    // ── POST /api/napas/callback ─────────────────────────────────────────────

    /// <summary>
    /// Nhận callback async từ NAPAS xác nhận kết quả giao dịch giải ngân.
    /// </summary>
    [HttpPost("callback")]
    public async Task<IActionResult> ReceiveCallback()
    {
        // 1. Đọc raw body để verify signature
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        // 2. Verify HMAC signature từ NAPAS
        var signatureHeader = Request.Headers["X-Napas-Signature"].FirstOrDefault();

        if (!VerifySignature(rawBody, signatureHeader))
        {
            _logger.LogWarning("[NAPAS-CALLBACK] Invalid or missing signature. IP={IP}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { success = false, message = "Invalid signature" });
        }

        // 3. Parse payload
        NapasCallbackPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<NapasCallbackPayload>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[NAPAS-CALLBACK] Invalid JSON payload.");
            return BadRequest(new { success = false, message = "Invalid JSON payload" });
        }

        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.ReferenceId) ||
            string.IsNullOrWhiteSpace(payload.Status))
        {
            return BadRequest(new { success = false, message = "ReferenceId and Status are required." });
        }

        _logger.LogInformation(
            "[NAPAS-CALLBACK] Received. ReferenceId={ReferenceId} Status={Status} NapasTxId={NapasTxId}",
            payload.ReferenceId, payload.Status, payload.NapasTransactionId);

        // 4. Delegate to gateway service (handles idempotency + DB update)
        var result = await _gatewayService.HandleCallbackAsync(payload);

        if (!result.IsSuccess)
        {
            _logger.LogError("[NAPAS-CALLBACK] HandleCallback failed: {Error}", result.Error);
            // Trả 200 để NAPAS không retry liên tục với lỗi business logic.
            // Trả 400 chỉ khi là lỗi data mà NAPAS nên biết.
            return NotFound(new { success = false, message = result.Error });
        }

        // 5. Nếu NAPAS báo FAILED → tạo PendingApprovalRequest để Approver xử lý manual
        if (!result.Value.IsSuccess)
        {
            await CreateFailedDisbursementAlertAsync(payload, result.Value.DisbursementId);
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                disbursementId = result.Value.DisbursementId,
                newStatus      = result.Value.NewStatus
            }
        });
    }

    // ── GET /api/napas/disbursements/{referenceId}/status ───────────────────

    /// <summary>
    /// Query trạng thái một giao dịch giải ngân theo NapasReferenceId.
    /// Dùng khi callback không đến sau thời gian chờ (timeout scenario).
    /// </summary>
    [HttpGet("disbursements/{referenceId}/status")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,LoanOfficer")]
    public async Task<IActionResult> QueryDisbursementStatus(string referenceId)
    {
        // Tìm trong DB trước
        var disbursement = await _db.LoanDisbursements
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.NapasReferenceId == referenceId);

        if (disbursement is null)
            return NotFound(new { success = false, message = $"No disbursement with ReferenceId '{referenceId}'." });

        // Nếu đã Completed/Failed, trả DB state — không cần gọi NAPAS
        if (disbursement.DisbursementStatus != DisbursementStatus.Pending)
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    disbursementId     = disbursement.Id,
                    napasReferenceId   = referenceId,
                    disbursementStatus = disbursement.DisbursementStatus,
                    napasTransactionId = disbursement.NapasTransactionId,
                    confirmedAt        = disbursement.GatewayConfirmedAt,
                    errorMessage       = disbursement.GatewayErrorMessage,
                    source             = "database"
                }
            });
        }

        // Nếu vẫn Pending → query NAPAS để lấy kết quả mới nhất
        var queryResult = await _gatewayService.QueryTransactionStatusAsync(referenceId);

        if (!queryResult.IsSuccess)
            return StatusCode(502, new { success = false, message = queryResult.Error });

        return Ok(new
        {
            success = true,
            data = new
            {
                disbursementId     = disbursement.Id,
                napasReferenceId   = referenceId,
                disbursementStatus = queryResult.Value.IsConfirmedImmediately
                    ? DisbursementStatus.Completed
                    : DisbursementStatus.Pending,
                napasTransactionId = queryResult.Value.TransactionId,
                confirmedAt        = queryResult.Value.ProcessedAt,
                source             = "napas_query"
            }
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private bool VerifySignature(string rawBody, string? signatureHeader)
    {
        // Development mode: nếu CallbackSecret chưa cấu hình → bỏ qua verify (dev only)
        if (string.IsNullOrWhiteSpace(_options.CallbackSecret) ||
            _options.CallbackSecret.StartsWith("SET_VIA", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[NAPAS-CALLBACK] CallbackSecret not configured — skipping signature verify (dev mode only).");
            return true;
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        // Chỉ real NapasPaymentGatewayService mới có VerifyCallbackSignature
        // Mock service verify bằng cách check prefix "MOCK-" trong signatureHeader
        if (_gatewayService is NapasPaymentGatewayService realService)
            return realService.VerifyCallbackSignature(rawBody, signatureHeader);

        // Mock mode: chấp nhận header "MOCK-VALID" để test callback flow
        return signatureHeader.Equals("MOCK-VALID", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateFailedDisbursementAlertAsync(NapasCallbackPayload payload, int disbursementId)
    {
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId   = 1, // system
                ActionCode    = "NAPAS_DISBURSEMENT_FAILED",
                EntityType    = "LoanDisbursement",
                EntityId      = disbursementId,
                NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    referenceId        = payload.ReferenceId,
                    napasTransactionId = payload.NapasTransactionId,
                    errorCode          = payload.ErrorCode,
                    errorMessage       = payload.ErrorMessage,
                    processedAt        = payload.ProcessedAt
                })
            });
            await _db.SaveChangesAsync();
            _logger.LogError(
                "[NAPAS-CALLBACK] Disbursement FAILED. DisbursementId={DisbursementId} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
                disbursementId, payload.ErrorCode, payload.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NAPAS-CALLBACK] Failed to create alert for failed disbursement {DisbursementId}.", disbursementId);
        }
    }
}
