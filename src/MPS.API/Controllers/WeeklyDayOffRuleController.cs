using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.WeeklyDayOffRule.Commands;
using MPS.Application.Features.Attendance.WeeklyDayOffRule.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/weekly-day-off-rules")]
[Authorize]
public class WeeklyDayOffRuleController : ControllerBase
{
    private readonly IMediator _mediator;

    public WeeklyDayOffRuleController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.WeeklyDayOffReadAll)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive,
        [FromQuery] string? applyScope,
        [FromQuery] DateOnly? effectiveOn,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetWeeklyDayOffRulesQuery(includeInactive, applyScope, effectiveOn),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.WeeklyDayOffReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWeeklyDayOffRuleByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("resolve")]
    [HasPermission(Permissions.WeeklyDayOffReadAll)]
    public async Task<IActionResult> Resolve(
        [FromQuery] int employeeInformationId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        if (employeeInformationId <= 0)
            return BadRequest(new { error = "employeeInformationId must be greater than 0." });
        var result = await _mediator.Send(new ResolveWeeklyDayOffQuery(employeeInformationId, date), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.WeeklyDayOffCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWeeklyDayOffRuleRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateWeeklyDayOffRuleCommand(
            request.Name,
            request.Description,
            request.DaysOfWeek,
            request.ApplyScope,
            request.TargetDepartmentIds,
            request.TargetEmployeeIds,
            request.EffectiveFrom,
            request.EffectiveTo), cancellationToken);
        return Created($"/api/weekly-day-off-rules/{id}", new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.WeeklyDayOffUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateWeeklyDayOffRuleRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateWeeklyDayOffRuleCommand(
            id,
            request.Name,
            request.Description,
            request.DaysOfWeek,
            request.ApplyScope,
            request.TargetDepartmentIds,
            request.TargetEmployeeIds,
            request.EffectiveFrom,
            request.EffectiveTo), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.WeeklyDayOffDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteWeeklyDayOffRuleCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
