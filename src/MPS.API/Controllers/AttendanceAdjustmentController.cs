using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;
using MPS.Application.Features.Attendance.AttendanceAdjustment.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/attendance-adjustments")]
[Authorize]
public class AttendanceAdjustmentController : ControllerBase
{
    private readonly IMediator _mediator;

    public AttendanceAdjustmentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.AttendanceAdjustmentReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeInformationId,
        [FromQuery] AdjustmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAttendanceAdjustmentsQuery(
            employeeInformationId, status, page, size), cancellationToken);
        return Ok(result);
    }

    /// <summary>NV xem đơn AttendanceAdjustment của chính mình (scope theo current user).</summary>
    [HttpGet("me")]
    [HasPermission(Permissions.AttendanceAdjustmentCreateSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] AdjustmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyAttendanceAdjustmentsQuery(status, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.AttendanceAdjustmentReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAttendanceAdjustmentByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.AttendanceAdjustmentCreateSelf)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAttendanceAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateAttendanceAdjustmentCommand(
            request.AttendanceRecordId, request.NewStatus,
            request.NewCheckInAt, request.NewCheckOutAt,
            request.Reason), cancellationToken);
        return Created($"/api/attendance-adjustments/{id}", new { id });
    }

    [HttpPut("{id:int}/approve")]
    [HasPermission(Permissions.AttendanceAdjustmentApproveAll)]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] ReviewAttendanceAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new ApproveAttendanceAdjustmentCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/reject")]
    [HasPermission(Permissions.AttendanceAdjustmentApproveAll)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] ReviewAttendanceAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new RejectAttendanceAdjustmentCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("/api/attendance/{employeeInformationId:int}/{workDate}/{shiftTemplateId:int}/override")]
    [HasPermission(Permissions.AttendanceAdjustmentOverrideAll)]
    public async Task<IActionResult> Override(
        int employeeInformationId,
        DateOnly workDate,
        int shiftTemplateId,
        [FromBody] OverrideAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new OverrideAttendanceCommand(
            employeeInformationId, workDate, shiftTemplateId, request.NewStatus,
            request.NewCheckInAt, request.NewCheckOutAt,
            request.OverrideReason), cancellationToken);
        return Created($"/api/attendance-adjustments/{id}", new { id });
    }
}
