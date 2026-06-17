using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.ShiftTemplate.Commands;
using MPS.Application.Features.Attendance.ShiftTemplate.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/shift-templates")]
[Authorize]
public class ShiftTemplateController : ControllerBase
{
    private readonly IMediator _mediator;

    public ShiftTemplateController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.ShiftTemplateReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetShiftTemplatesQuery(isActive, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.ShiftTemplateReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetShiftTemplateByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.ShiftTemplateCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateShiftTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateShiftTemplateCommand(
            request.Code, request.Name, request.StartTime, request.EndTime,
            request.BreakStartTime, request.BreakEndTime,
            request.StandardHours, request.StandardDays,
            request.WorkRateNormal, request.WorkRateWeeklyRest, request.WorkRateHoliday,
            request.IsNightShift,
            request.AppliedAttendanceRuleId, request.Notes), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ShiftTemplateUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateShiftTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateShiftTemplateCommand(
            id, request.Code, request.Name, request.StartTime, request.EndTime,
            request.BreakStartTime, request.BreakEndTime,
            request.StandardHours, request.StandardDays,
            request.IsNightShift, request.AppliedAttendanceRuleId,
            request.IsActive, request.Notes), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/pay-rates")]
    [HasPermission(Permissions.ShiftTemplateUpdateAll)]
    public async Task<IActionResult> UpdatePayRates(
        int id,
        [FromBody] UpdateShiftPayRatesRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateShiftPayRatesCommand(
            id,
            request.WorkRateNormal,
            request.WorkRateWeeklyRest,
            request.WorkRateHoliday), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:int}/reapply-rule")]
    [HasPermission(Permissions.ShiftTemplateUpdateAll)]
    public async Task<IActionResult> ReapplyRule(
        int id,
        [FromBody] ReapplyAttendanceRuleRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new ReapplyAttendanceRuleCommand(id, request.AttendanceRuleId), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ShiftTemplateDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteShiftTemplateCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
