using LoanFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    public DocumentsController(IDocumentService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Upload([FromBody] UploadRequest req)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _service.UploadAsync(req.LoanId, req.CustomerId, req.DocumentType,
            req.FileName, req.FilePath, req.FileSize, req.MimeType, userId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id}/verify")]
    [Authorize(Roles = "Admin,Manager,Approver")]
    public async Task<IActionResult> Verify(int id, [FromBody] VerifyDocRequest req)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _service.VerifyAsync(id, req.Status, req.Notes, userId);
        return result.IsSuccess ? Ok(new { message = "Document verified." }) : BadRequest(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? loanId, [FromQuery] int? customerId)
        => Ok(await _service.GetDocumentsAsync(loanId, customerId));

    public class UploadRequest
    {
        public int? LoanId { get; set; }
        public int? CustomerId { get; set; }
        public string DocumentType { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public string? MimeType { get; set; }
    }

    public class VerifyDocRequest
    {
        public string Status { get; set; } = "";    // Verified | Rejected
        public string? Notes { get; set; }
    }
}
