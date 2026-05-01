using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoanFlow.API.Controllers;

/// <summary>
/// NAPAS Core Group — Transaction Query, Merchant Info, Reconciliation, QR Code.
/// Tất cả endpoints yêu cầu đăng nhập; một số endpoint giới hạn theo role.
/// </summary>
[ApiController]
[Route("api/napas")]
[Authorize]
public class NapasCoreController : ControllerBase
{
    private readonly INapasCoreService _napas;
    private readonly ILogger<NapasCoreController> _logger;

    public NapasCoreController(INapasCoreService napas, ILogger<NapasCoreController> logger)
    {
        _napas  = napas;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TRANSACTION QUERY
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Lấy chi tiết giao dịch theo NAPAS Transaction ID</summary>
    [HttpGet("transactions/{napasTransactionId}")]
    [Authorize(Roles = "Admin,Manager,LoanOfficer")]
    public async Task<IActionResult> GetTransaction(string napasTransactionId)
    {
        var result = await _napas.GetTransactionAsync(napasTransactionId);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : NotFound(new { success = false, message = result.Error });
    }

    /// <summary>Lấy chi tiết giao dịch theo ReferenceId hệ thống (LF-...)</summary>
    [HttpGet("transactions/by-reference/{referenceId}")]
    [Authorize(Roles = "Admin,Manager,LoanOfficer")]
    public async Task<IActionResult> GetTransactionByReference(string referenceId)
    {
        var result = await _napas.GetTransactionByReferenceAsync(referenceId);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : NotFound(new { success = false, message = result.Error });
    }

    /// <summary>Tìm kiếm giao dịch NAPAS theo ngày / trạng thái / loại</summary>
    [HttpPost("transactions/search")]
    [Authorize(Roles = "Admin,Manager,LoanOfficer")]
    public async Task<IActionResult> SearchTransactions([FromBody] NapasTransactionSearchRequest request)
    {
        if (request.PageSize > 100) request.PageSize = 100;
        var result = await _napas.SearchTransactionsAsync(request);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : BadRequest(new { success = false, message = result.Error });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MERCHANT INFO
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Thông tin Merchant đăng ký trên NAPAS</summary>
    [HttpGet("merchant")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetMerchantInfo()
    {
        var result = await _napas.GetMerchantInfoAsync();
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : StatusCode(502, new { success = false, message = result.Error });
    }

    /// <summary>Cài đặt Merchant: hạn mức, kênh giao dịch, callback</summary>
    [HttpGet("merchant/settings")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetMerchantSettings()
    {
        var result = await _napas.GetMerchantSettingsAsync();
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : StatusCode(502, new { success = false, message = result.Error });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RECONCILIATION
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Danh sách file đối soát NAPAS theo ngày (yyyy-MM-dd)</summary>
    [HttpGet("reconciliation/files")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ListRecoFiles([FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var businessDate))
            return BadRequest(new { success = false, message = "Invalid date format. Use yyyy-MM-dd." });

        var result = await _napas.ListRecoFilesAsync(businessDate);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : StatusCode(502, new { success = false, message = result.Error });
    }

    /// <summary>Tải nội dung file đối soát (parse thành records)</summary>
    [HttpGet("reconciliation/files/{date}/download")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DownloadRecoFile(string date, [FromQuery] string type = "settlement")
    {
        if (!DateOnly.TryParse(date, out var businessDate))
            return BadRequest(new { success = false, message = "Invalid date format. Use yyyy-MM-dd." });

        var result = await _napas.DownloadRecoFileAsync(businessDate, type);
        if (!result.IsSuccess)
            return StatusCode(502, new { success = false, message = result.Error });

        // Trả raw CSV nếu client muốn download file
        if (Request.Headers.Accept.ToString().Contains("text/csv"))
            return File(System.Text.Encoding.UTF8.GetBytes(result.Value!.RawCsv), "text/csv",
                        $"NAPAS_{date}_{type}.csv");

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// Khớp lệnh file NAPAS vs giao dịch nội bộ.
    /// Unmatched records → tạo ReconciliationException (ExceptionType = DisbursementMismatch).
    /// </summary>
    [HttpPost("reconciliation/files/{date}/match")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> MatchRecoFile(string date)
    {
        if (!DateOnly.TryParse(date, out var businessDate))
            return BadRequest(new { success = false, message = "Invalid date format. Use yyyy-MM-dd." });

        var userId = int.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value, out var uid) ? uid : 1;
        var result = await _napas.MatchRecoFileAsync(businessDate, userId);

        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : StatusCode(502, new { success = false, message = result.Error });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // QR CODE (VietQR)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo VietQR code (EMV QRCPS offline) để khách hàng thanh toán.
    /// Trả về QR string + Base64 PNG + VietQR URL.
    /// </summary>
    [HttpPost("qr/generate")]
    [Authorize(Roles = "Admin,Manager,LoanOfficer,Collector")]
    public async Task<IActionResult> GenerateQr([FromBody] NapasQrRequest request)
    {
        if (request.Amount < 0)
            return BadRequest(new { success = false, message = "Amount must be >= 0." });
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { success = false, message = "Description is required." });

        var result = await _napas.GenerateQrAsync(request);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : BadRequest(new { success = false, message = result.Error });
    }

    /// <summary>Giải mã QR string theo chuẩn EMV QRCPS → thông tin giao dịch</summary>
    [HttpPost("qr/decode")]
    [Authorize(Roles = "Admin,Manager,LoanOfficer,Collector")]
    public async Task<IActionResult> DecodeQr([FromBody] DecodeQrRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QrString))
            return BadRequest(new { success = false, message = "qrString is required." });

        var result = await _napas.DecodeQrAsync(request.QrString);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : BadRequest(new { success = false, message = result.Error });
    }

    /// <summary>
    /// Lấy / tạo VietQR code cho khoản vay.
    /// Amount tự động pre-fill theo kỳ trả nợ đến hạn gần nhất.
    /// </summary>
    [HttpGet("qr/loan/{loanId:int}")]
    [Authorize(Roles = "Admin,Manager,LoanOfficer,Collector")]
    public async Task<IActionResult> GetLoanQr(int loanId)
    {
        var result = await _napas.GetOrCreateLoanQrAsync(loanId);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : NotFound(new { success = false, message = result.Error });
    }
}

/// <summary>Request body cho endpoint decode QR</summary>
public class DecodeQrRequest
{
    public string QrString { get; set; } = default!;
}
