using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;
using LoanFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    private readonly LoanFlowDbContext _db;
    private readonly ILoanService _loanService;

    public WebhookController(LoanFlowDbContext db, ILoanService loanService)
    {
        _db = db;
        _loanService = loanService;
    }

    public class PaymentWebhookPayload
    {
        public string VirtualAccountNo { get; set; } = default!;
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = default!;
        public string Description { get; set; } = default!;
    }

    [HttpPost("bank-transfer")]
    public async Task<IActionResult> ReceiveBankTransfer([FromBody] PaymentWebhookPayload payload)
    {
        // 1. Idempotency Check
        if (await _db.Payments.AnyAsync(p => p.ExternalReferenceId == payload.TransactionId))
        {
            return Ok(new { success = true, message = "Transaction already processed" });
        }

        // 2. Lookup Loan by VA
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.VirtualAccountNo == payload.VirtualAccountNo);
        if (loan == null)
        {
            return NotFound(new { success = false, message = "Virtual account not found" });
        }

        if (payload.Amount <= 0)
        {
            return BadRequest(new { success = false, message = "Amount must be positive" });
        }

        // 3. Record Payment automatically (System UserId = 1 or special Admin ID, for MVP we use 1)
        var req = new RecordPaymentRequest
        {
            PaymentDate = DateOnly.FromDateTime(DateTime.Now), // Usually we get this from webhook, fallback to Now
            Amount = payload.Amount,
            PaymentMethod = "BankTransfer",
            Remarks = $"Webhook: {payload.Description} (Tx: {payload.TransactionId})"
        };

        var result = await _loanService.RecordPaymentAsync(loan.Id, req, 1);

        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, error = result.Error });
        }

        // Update ExternalReferenceId to ensure we don't process it again
        // Actually RecordPaymentAsync doesn't take ExternalReferenceId yet, so we have to fetch the latest payment and update it.
        // For MVP, we know the latest payment for this loan with this amount was just created.
        var payment = await _db.Payments
            .Where(p => p.LoanId == loan.Id)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

        if (payment != null)
        {
            payment.ExternalReferenceId = payload.TransactionId;
            await _db.SaveChangesAsync();
        }

        return Ok(new { success = true, data = new { message = "Payment recorded successfully", loanId = loan.Id } });
    }
}
