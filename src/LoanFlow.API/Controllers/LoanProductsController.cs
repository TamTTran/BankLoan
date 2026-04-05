using LoanFlow.Application.DTOs.LoanProduct;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/loan-products")]
[Authorize]
public class LoanProductsController : ControllerBase
{
    private readonly ILoanProductService _service;
    public LoanProductsController(ILoanProductService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Danh sách sản phẩm vay đang hoạt động</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllActiveAsync();
        return Ok(result);
    }

    /// <summary>Chi tiết sản phẩm vay</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { message = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Preview installment trước khi tạo hồ sơ</summary>
    [HttpPost("preview-installment")]
    public async Task<IActionResult> PreviewInstallment([FromBody] InstallmentPreviewRequest request)
    {
        var result = await _service.PreviewInstallmentAsync(request);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Tạo sản phẩm vay mới (chỉ Admin)</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateLoanProductRequest request)
    {
        var result = await _service.CreateAsync(request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Cập nhật sản phẩm vay (chỉ Admin)</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateLoanProductRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(result.Value);
    }
}
