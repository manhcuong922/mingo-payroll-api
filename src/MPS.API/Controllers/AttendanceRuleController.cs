using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.AttendanceRule.Commands;
using MPS.Application.Features.Attendance.AttendanceRule.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/attendance-rules")]
[Authorize]
public class AttendanceRuleController : ControllerBase
{
    private readonly IMediator _mediator;

    public AttendanceRuleController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.AttendanceRuleReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAttendanceRulesQuery(isActive, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.AttendanceRuleReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAttendanceRuleByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.AttendanceRuleCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAttendanceRuleRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateAttendanceRuleCommand(
            request.Name,
            request.Description,
            request.WorkRateNormal,
            request.WorkRateWeeklyRest,
            request.WorkRateHoliday), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.AttendanceRuleUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateAttendanceRuleRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateAttendanceRuleCommand(
            id,
            request.Name,
            request.Description,
            request.WorkRateNormal,
            request.WorkRateWeeklyRest,
            request.WorkRateHoliday,
            request.IsActive), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.AttendanceRuleDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteAttendanceRuleCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
