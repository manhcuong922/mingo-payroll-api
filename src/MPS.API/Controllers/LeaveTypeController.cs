using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.LeaveType.Commands;
using MPS.Application.Features.Attendance.LeaveType.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/leave-types")]
[Authorize]
public class LeaveTypeController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeaveTypeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.LeaveTypeReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLeaveTypesQuery(isActive, search, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.LeaveTypeReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLeaveTypeByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.LeaveTypeCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLeaveTypeRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateLeaveTypeCommand(
            request.Code,
            request.Name,
            request.Description,
            request.IsPaid,
            request.PaidRate,
            request.MaxDaysPerRequest,
            request.AllowEmployeeSelection,
            request.MaxDaysPerYear,
            request.MaxDaysPerMonth,
            request.MaxDaysPerWeek,
            request.ApplyScope,
            request.TargetDepartmentIds,
            request.TargetEmployeeIds,
            request.RequireDocument,
            request.DeductsBalance,
            request.MinNoticeDaysForShort,
            request.MinNoticeDaysForMedium,
            request.MinNoticeDaysForLong,
            request.AutoFallbackToUnpaid,
            request.AllowRetroactive,
            request.IsActive), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.LeaveTypeUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateLeaveTypeRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateLeaveTypeCommand(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.IsPaid,
            request.PaidRate,
            request.MaxDaysPerRequest,
            request.AllowEmployeeSelection,
            request.MaxDaysPerYear,
            request.MaxDaysPerMonth,
            request.MaxDaysPerWeek,
            request.ApplyScope,
            request.TargetDepartmentIds,
            request.TargetEmployeeIds,
            request.RequireDocument,
            request.DeductsBalance,
            request.MinNoticeDaysForShort,
            request.MinNoticeDaysForMedium,
            request.MinNoticeDaysForLong,
            request.AutoFallbackToUnpaid,
            request.AllowRetroactive,
            request.IsActive), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.LeaveTypeDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteLeaveTypeCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
