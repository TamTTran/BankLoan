using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    private readonly LoanFlowDbContext _db;
    private readonly ILoanPaymentService _loanPaymentService;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        LoanFlowDbContext db,
        ILoanPaymentService loanPaymentService,
        IConfiguration config,
        ILogger<WebhookController> logger)
    {
        _db = db;
        _loanPaymentService = loanPaymentService;
        _config = config;
        _logger = logger;
    }

    public class PaymentWebhookPayload
    {
        public string VirtualAccountNo { get; set; } = default!;
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = default!;
        public string Description { get; set; } = default!;
    }

    [HttpPost("bank-transfer")]
    public async Task<IActionResult> ReceiveBankTransfer()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        var signatureHeader = Request.Headers["X-Signature-256"].FirstOrDefault();
        if (string.IsNullOrEmpty(signatureHeader))
            return Unauthorized(new { success = false, message = "Missing signature" });

        var secret = _config["Webhook:Secret"];
        if (string.IsNullOrEmpty(secret) || secret == "SET_VIA_ENV_OR_USER_SECRETS")
            return StatusCode(500, new { success = false, message = "Webhook secret not configured validly" });

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expectedSignature = Convert.ToHexString(hashBytes).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature), 
            Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant())))
        {
            return Unauthorized(new { success = false, message = "Invalid signature" });
        }

        var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (payload == null || string.IsNullOrWhiteSpace(payload.TransactionId) || string.IsNullOrWhiteSpace(payload.VirtualAccountNo))
            return BadRequest(new { success = false, message = "Invalid payload" });

        var systemActorId = await ResolveWebhookSystemActorIdAsync();
        if (systemActorId is null)
            return StatusCode(500, new { success = false, message = "Webhook system actor is not configured" });

        // Wrap Idempotency check AND Payment Recording in one atomic transaction
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. Idempotency Check (FOR UPDATE would be better but we rely on unique DB constraints + transaction isolation)
            if (await _db.Payments.AnyAsync(p => p.ExternalReferenceId == payload.TransactionId))
            {
                await tx.RollbackAsync();
                return Ok(new { success = true, message = "Transaction already processed" });
            }

            // 2. Lookup Loan by VA
            var loan = await _db.Loans.FirstOrDefaultAsync(l => l.VirtualAccountNo == payload.VirtualAccountNo);
            if (loan == null)
            {
                await tx.RollbackAsync();
                return NotFound(new { success = false, message = "Virtual account not found" });
            }

            if (payload.Amount <= 0)
            {
                await tx.RollbackAsync();
                return BadRequest(new { success = false, message = "Amount must be positive" });
            }

            // 3. Record Payment automatically
            var req = new RecordPaymentRequest
            {
                PaymentDate = DateOnly.FromDateTime(DateTime.Now),
                Amount = payload.Amount,
                PaymentMethod = "BankTransfer",
                Remarks = $"Webhook: {payload.Description} (Tx: {payload.TransactionId})",
                ExternalReferenceId = payload.TransactionId
            };

            var result = await _loanPaymentService.RecordPaymentAsync(loan.Id, req, systemActorId.Value);

            if (!result.IsSuccess)
            {
                await tx.RollbackAsync();
                return BadRequest(new { success = false, error = result.Error });
            }

            await tx.CommitAsync();
            return Ok(new { success = true, data = new { message = "Payment recorded successfully", loanId = loan.Id } });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(
                ex,
                "Bank-transfer webhook processing failed for transaction {TransactionId} and virtual account {VirtualAccountNo}.",
                payload.TransactionId,
                payload.VirtualAccountNo);

            return StatusCode(500, new
            {
                success = false,
                message = "Processing failed",
                traceId = HttpContext.TraceIdentifier
            });
        }
    }

    private async Task<int?> ResolveWebhookSystemActorIdAsync()
    {
        var username = _config["Webhook:SystemActorUsername"];
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogError("Webhook:SystemActorUsername is not configured.");
            return null;
        }

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Username == username)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            _logger.LogError("Webhook system actor user {Username} was not found.", username);
            return null;
        }

        return user.Id;
    }
}
