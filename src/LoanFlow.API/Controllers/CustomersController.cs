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
    private readonly ICollateralService _collateralService;
    private readonly ICreditCheckService _creditCheckService;
    private readonly ICustomerLeadService _leadService;

    public CustomersController(
        ICustomerService service,
        ICollateralService collateralService,
        ICreditCheckService creditCheckService,
        ICustomerLeadService leadService)
    {
        _service = service;
        _collateralService = collateralService;
        _creditCheckService = creditCheckService;
        _leadService = leadService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)!;

    /// <summary>GET /customers — CUS-003</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> GetList([FromQuery] CustomerFilterRequest filter)
        => Ok(await _service.GetListAsync(filter));

    /// <summary>GET /customers/{id} — CUS-004</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { success = false, error = new { code = "APP-VAL-003", message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /customers — CUS-001</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var result = await _service.CreateAsync(request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, new { success = true, data = result.Value });
    }

    /// <summary>PATCH /customers/{id} — CUS-002</summary>
    [HttpPatch("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /customers/{id}/check-credit — CUS-005</summary>
    [HttpPost("{id:int}/check-credit")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer}")]
    public async Task<IActionResult> CheckCreditStatus(int id)
    {
        var result = await _service.CheckCreditStatusAsync(id, CurrentUserId);
        if (!result.IsSuccess) return NotFound(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /customers/{id}/collaterals</summary>
    [HttpGet("{id:int}/collaterals")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver}")]
    public async Task<IActionResult> GetCollaterals(int id)
    {
        var collaterals = await _collateralService.GetByCustomerIdAsync(id);
        return Ok(new { success = true, data = collaterals });
    }

    /// <summary>GET /customers/{id}/credit-checks</summary>
    [HttpGet("{id:int}/credit-checks")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Approver},{Roles.Manager}")]
    public async Task<IActionResult> GetCreditCheckHistory(int id)
    {
        var history = await _creditCheckService.GetHistoryAsync(id);
        return Ok(new { success = true, data = history });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Customer Intelligence Endpoints
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>POST /customers/{id}/assign — Chuyển giao nhân viên phụ trách</summary>
    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignCustomerRequest request)
    {
        var result = await _service.AssignAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /customers/{id}/care-notes — Thêm ghi chú chăm sóc</summary>
    [HttpPost("{id:int}/care-notes")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> AddCareNote(int id, [FromBody] CreateCareNoteRequest request)
    {
        var result = await _service.AddCareNoteAsync(id, request, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { success = false, error = new { message = result.Error } });
        return CreatedAtAction(nameof(GetCareNotes), new { id }, new { success = true, data = result.Value });
    }

    /// <summary>GET /customers/{id}/care-notes — Xem lịch sử chăm sóc</summary>
    [HttpGet("{id:int}/care-notes")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Collector},{Roles.Manager}")]
    public async Task<IActionResult> GetCareNotes(int id)
    {
        var notes = await _service.GetCareNotesAsync(id);
        return Ok(new { success = true, data = notes });
    }

    /// <summary>GET /customers/leads — Lead Board cho nhân viên hiện tại</summary>
    [HttpGet("leads")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.LoanOfficer},{Roles.Manager}")]
    public async Task<IActionResult> GetMyLeads()
    {
        // Manager/Admin sees all; LoanOfficer sees only their assigned customers
        int? scopeUserId = (CurrentRole == Roles.Admin || CurrentRole == Roles.Manager)
            ? null
            : CurrentUserId;

        var leads = await _leadService.GetLeadBoardAsync(scopeUserId);
        return Ok(new { success = true, data = leads });
    }

    /// <summary>GET /customers/leads/all — Lead Board toàn bộ (Manager/Admin only)</summary>
    [HttpGet("leads/all")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    public async Task<IActionResult> GetAllLeads()
    {
        var leads = await _leadService.GetLeadBoardAsync(null);
        return Ok(new { success = true, data = leads });
    }
}
