using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Application.Features.Attendance.EmployeeShift.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/employee-shifts")]
[Authorize]
public class EmployeeShiftController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeeShiftController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("daily/{workDate}")]
    [HasPermission(Permissions.EmployeeShiftReadAll)]
    public async Task<IActionResult> GetDailyRoster(DateOnly workDate, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDailyShiftRosterQuery(workDate), cancellationToken);
        return Ok(result);
    }

    [HttpGet("roster")]
    [HasPermission(Permissions.EmployeeShiftReadAll)]
    public async Task<IActionResult> GetRoster(
        [FromQuery(Name = "employeeIds")] int[] employeeIds,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var ids = (employeeIds ?? Array.Empty<int>()).ToList();
        var result = await _mediator.Send(new GetShiftRosterQuery(ids, fromDate, toDate), cancellationToken);
        return Ok(result);
    }

    [HttpPost("batch")]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    public async Task<IActionResult> BatchUpdate(
        [FromBody] BatchUpdateShiftAssignmentsRequest request,
        CancellationToken cancellationToken)
    {
        var operations = (request.Operations ?? Array.Empty<BatchShiftOperationRequest>())
            .Select(o => new BatchShiftOperation(o.Kind, o.EmployeeInformationId, o.WorkDate, o.ShiftTemplateId))
            .ToList();

        var result = await _mediator.Send(
            new BatchUpdateShiftAssignmentsCommand(
                operations,
                request.ConflictPolicy,
                SourceScheduleTemplateId: null,
                AcknowledgeWeeklyOvertimeWarnings: request.AcknowledgeWeeklyOvertimeWarnings,
                IncludeRestDaysAndHolidays: request.IncludeRestDaysAndHolidays),
            cancellationToken);
        return Ok(result);
    }
}
