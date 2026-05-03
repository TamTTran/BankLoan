using LoanFlow.API.Filters;
using LoanFlow.Application.DTOs.EWallet;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

/// <summary>
/// Quản lý liên kết ví điện tử (MoMo, VNPay) cho khách hàng.
///
/// Endpoints:
///   POST   /api/customers/{customerId}/ewallets/link        — Khởi tạo liên kết ví (gửi OTP)
///   POST   /api/customers/{customerId}/ewallets/verify-otp  — Xác thực OTP liên kết
///   DELETE /api/customers/{customerId}/ewallets/{id}         — Hủy liên kết ví
///   GET    /api/customers/{customerId}/ewallets              — Danh sách ví đã liên kết
///   POST   /api/loans/{loanId}/pay-from-ewallet              — Thanh toán bằng ví [Idempotent]
///   POST   /api/loans/{loanId}/disburse-to-ewallet           — Giải ngân vào ví [Idempotent]
///
/// Giới hạn giao dịch: 100.000.000 VND / lần.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class EWalletController : ControllerBase
{
    private readonly IEWalletService _ewalletService;

    public EWalletController(IEWalletService ewalletService)
    {
        _ewalletService = ewalletService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Link Wallet ─────────────────────────────────────────────────────────

    /// <summary>POST /api/customers/{customerId}/ewallets/link — Khởi tạo liên kết ví (gửi OTP)</summary>
    [HttpPost("customers/{customerId:int}/ewallets/link")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> LinkWallet(int customerId, [FromBody] LinkEWalletRequest request)
    {
        var result = await _ewalletService.LinkWalletAsync(customerId, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value, message = "OTP đã được gửi đến ví. Vui lòng xác thực." });
    }

    // ── Verify OTP ──────────────────────────────────────────────────────────

    /// <summary>POST /api/customers/{customerId}/ewallets/verify-otp — Xác thực OTP liên kết</summary>
    [HttpPost("customers/{customerId:int}/ewallets/verify-otp")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> VerifyOtp(int customerId, [FromBody] VerifyEWalletOtpRequest request)
    {
        var result = await _ewalletService.VerifyLinkingOtpAsync(customerId, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value, message = "Ví đã được liên kết thành công." });
    }

    // ── Unlink Wallet ───────────────────────────────────────────────────────

    /// <summary>DELETE /api/customers/{customerId}/ewallets/{id} — Hủy liên kết ví</summary>
    [HttpDelete("customers/{customerId:int}/ewallets/{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> UnlinkWallet(int customerId, int id)
    {
        var result = await _ewalletService.UnlinkWalletAsync(customerId, id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = new { message = "Ví đã được hủy liên kết." } });
    }

    // ── List Wallets ────────────────────────────────────────────────────────

    /// <summary>GET /api/customers/{customerId}/ewallets — Danh sách ví đã liên kết</summary>
    [HttpGet("customers/{customerId:int}/ewallets")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Manager}")]
    public async Task<IActionResult> GetWallets(int customerId)
    {
        var wallets = await _ewalletService.GetByCustomerIdAsync(customerId);
        return Ok(new { success = true, data = wallets });
    }

    // ── Pay from E-Wallet ───────────────────────────────────────────────────

    /// <summary>POST /api/loans/{loanId}/pay-from-ewallet — Thanh toán khoản vay bằng ví</summary>
    [HttpPost("loans/{loanId:int}/pay-from-ewallet")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    [IdempotencyFilter]
    public async Task<IActionResult> PayFromWallet(int loanId, [FromBody] EWalletPaymentRequest request)
    {
        var result = await _ewalletService.PayFromWalletAsync(loanId, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    // ── Disburse to E-Wallet ────────────────────────────────────────────────

    /// <summary>POST /api/loans/{loanId}/disburse-to-ewallet — Giải ngân vào ví điện tử</summary>
    [HttpPost("loans/{loanId:int}/disburse-to-ewallet")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    [IdempotencyFilter]
    public async Task<IActionResult> DisburseToWallet(int loanId, [FromBody] EWalletDisbursementRequest request)
    {
        var result = await _ewalletService.DisburseToWalletAsync(loanId, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    // ── Query Transaction ────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/ewallet/transactions/{transactionId}?provider=MoMo
    /// Truy vấn trạng thái giao dịch ví điện tử từ provider.
    /// </summary>
    [HttpGet("ewallet/transactions/{transactionId}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Manager}")]
    public async Task<IActionResult> QueryTransaction(string transactionId, [FromQuery] string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { success = false, error = new { message = "Query param 'provider' (MoMo|VNPay) là bắt buộc." } });

        var result = await _ewalletService.QueryTransactionAsync(provider, transactionId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }
}
