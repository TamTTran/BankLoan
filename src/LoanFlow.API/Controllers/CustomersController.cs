using LoanFlow.Application.DTOs.Customer;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoanFlow.API.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    public CustomersController(ICustomerService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Danh sách khách hàng có phân trang và tìm kiếm</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] CustomerFilterRequest filter)
    {
        var result = await _service.GetListAsync(filter);
        return Ok(result);
    }

    /// <summary>Chi tiết khách hàng</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { message = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Tạo khách hàng mới</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var result = await _service.CreateAsync(request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Cập nhật thông tin khách hàng</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Xóa khách hàng (chỉ khi chưa có hồ sơ vay)</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return NoContent();
    }
}
