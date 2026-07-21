using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Department;
using MPS.Application.Features.Department.Commands;
using MPS.Application.Features.Department.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartmentController : ControllerBase
{
    private readonly IMediator _mediator;

    public DepartmentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.DepartmentReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DepartmentGetAllQuery(includeInactive, keyword), cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [HasPermission(Permissions.DepartmentReadAll)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new DepartmentGetPagedQuery(keyword, page, pageSize, includeInactive), cancellationToken);
        return Ok(result);
    }

    // Dropdown chọn tổ trưởng xưởng: chỉ nhân viên có tài khoản + role được phép làm tổ trưởng
    // (đã loại SystemAdmin/HR/Accountant/Director/Ban KH). keyword lọc theo tên.
    [HttpGet("eligible-managers")]
    [HasPermission(Permissions.DepartmentReadAll)]
    public async Task<IActionResult> GetEligibleManagers(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DepartmentEligibleManagersQuery(keyword), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.DepartmentReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DepartmentGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // Danh sách nhân viên thuộc 1 xưởng. Tổ trưởng xưởng chỉ xem được xưởng mình quản lý (scoped trong handler).
    [HttpGet("{id:int}/employees")]
    [HasAnyPermission(
        Permissions.EmployeeInformationReadAll,
        Permissions.EmployeeInformationReadDepartment)]
    public async Task<IActionResult> GetEmployees(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DepartmentEmployeesQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.DepartmentCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DepartmentCreateCommand(
            request.Code,
            request.Name,
            request.Description,
            request.ManagerId);

        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.DepartmentUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DepartmentUpdateCommand(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.ManagerId);

        var updated = await _mediator.Send(command, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.DepartmentDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DepartmentDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
