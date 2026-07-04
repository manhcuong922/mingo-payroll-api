using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Commands;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/overtime-slot-templates")]
[Authorize]
public class OvertimeSlotTemplateController : ControllerBase
{
    private readonly IMediator _mediator;

    public OvertimeSlotTemplateController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.OvertimeSlotTemplateReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetOvertimeSlotTemplatesQuery(isActive, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.OvertimeSlotTemplateReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOvertimeSlotTemplateByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Trả về mọi slot active. FE tự lọc theo tiêu chí hiển thị (VD: theo IsNightSlot,
    /// theo khung giờ). Slot ứng dụng cho mọi loại ngày; hệ số phân biệt qua 7 field multiplier.
    /// </summary>
    [HttpGet("active")]
    [HasPermission(Permissions.OvertimeSlotTemplateReadAll)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveOvertimeSlotTemplatesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.OvertimeSlotTemplateCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOvertimeSlotTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateOvertimeSlotTemplateCommand(
            request.StartTime,
            request.EndTime,
            request.IsNightSlot,
            request.MultiplierNormalDay,
            request.MultiplierWeeklyRestDay,
            request.MultiplierHolidayDay,
            request.MultiplierNormalNightNoPrevOt,
            request.MultiplierNormalNightWithPrevOt,
            request.MultiplierWeeklyRestNight,
            request.MultiplierHolidayNight,
            request.Description), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.OvertimeSlotTemplateUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateOvertimeSlotTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateOvertimeSlotTemplateCommand(
            id,
            request.StartTime,
            request.EndTime,
            request.IsNightSlot,
            request.MultiplierNormalDay,
            request.MultiplierWeeklyRestDay,
            request.MultiplierHolidayDay,
            request.MultiplierNormalNightNoPrevOt,
            request.MultiplierNormalNightWithPrevOt,
            request.MultiplierWeeklyRestNight,
            request.MultiplierHolidayNight,
            request.Description,
            request.IsActive), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.OvertimeSlotTemplateDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteOvertimeSlotTemplateCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
