using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.Attendance.Commands;
using MPS.Application.Features.Attendance.Attendance.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public AttendanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] int? employeeInformationId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetAttendanceSummaryQuery(fromDate, toDate, employeeInformationId, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{employeeInformationId:int}")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetEmployeeDetail(
        int employeeInformationId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetEmployeeAttendanceDetailQuery(employeeInformationId, fromDate, toDate), cancellationToken);
        return Ok(result);
    }

    [HttpGet("daily/{workDate}")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetDaily(DateOnly workDate, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDailyAttendanceQuery(workDate), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.AttendanceReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyAttendanceQuery(fromDate, toDate), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.AttendanceCreateAll)]
    public async Task<IActionResult> Record(
        [FromBody] RecordAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new RecordAttendanceCommand(
            request.EmployeeInformationId, request.WorkDate, request.ShiftTemplateId,
            request.CheckInTime, request.CheckOutTime, request.Status, request.Notes), cancellationToken);
        return Created($"/api/attendance/{request.EmployeeInformationId}/{request.WorkDate:yyyy-MM-dd}/{request.ShiftTemplateId}", new { id });
    }

    [HttpPost("bulk")]
    [HasPermission(Permissions.AttendanceCreateAll)]
    public async Task<IActionResult> BulkRecord(
        [FromBody] BulkRecordAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var rows = request.Records.Select(r => new BulkRecordAttendanceItem(
            r.EmployeeInformationId, r.ShiftTemplateId, r.CheckInTime,
            r.CheckOutTime, r.Status, r.Notes, r.OverrideReason)).ToList();
        var result = await _mediator.Send(new BulkRecordAttendanceCommand(request.WorkDate, rows), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{employeeInformationId:int}/{workDate}/{shiftTemplateId:int}")]
    [HasPermission(Permissions.AttendanceUpdateAll)]
    public async Task<IActionResult> Update(
        int employeeInformationId,
        DateOnly workDate,
        int shiftTemplateId,
        [FromBody] UpdateAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateAttendanceCommand(
            employeeInformationId, workDate, shiftTemplateId,
            request.CheckInTime, request.CheckOutTime, request.Status,
            request.Notes, request.OverrideReason), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }
}
