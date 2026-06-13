using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Compliance;
using MPS.Application.Features.Compliance.LaborLawConstants.Commands;
using MPS.Application.Features.Compliance.LaborLawConstants.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/compliance/labor-law-constants")]
public class LaborLawConstantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LaborLawConstantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.LaborLawConstantsReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllLaborLawConstantsQuery(includeArchived), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.LaborLawConstantsReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLaborLawConstantsByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active")]
    [HasPermission(Permissions.LaborLawConstantsReadAll)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveLaborLawConstantsQuery(), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active-at")]
    [HasPermission(Permissions.LaborLawConstantsReadAll)]
    public async Task<IActionResult> GetActiveAt([FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveLaborLawConstantsAtQuery(date), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.LaborLawConstantsCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLaborLawConstantsRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateLaborLawConstantsCommand(
            request.ValidFrom,
            request.ApplyImmediately,
            request.ProbationSalaryMinPercent,
            request.NightShiftSurchargePercent,
            request.StandardWorkingHoursPerDay,
            request.StandardWorkingDaysPerMonth,
            request.AnnualLeaveMinDays,
            request.OvertimeMaxHoursPerDay,
            request.OvertimeMaxHoursPerYear,
            request.LegalReference,
            request.Note), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.LaborLawConstantsUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateLaborLawConstantsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateLaborLawConstantsCommand(
            id,
            request.ValidFrom,
            request.ProbationSalaryMinPercent,
            request.NightShiftSurchargePercent,
            request.StandardWorkingHoursPerDay,
            request.StandardWorkingDaysPerMonth,
            request.AnnualLeaveMinDays,
            request.OvertimeMaxHoursPerDay,
            request.OvertimeMaxHoursPerYear,
            request.LegalReference,
            request.Note), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.LaborLawConstantsDeleteAll)]
    public async Task<IActionResult> DeletePending(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteLaborLawConstantsPendingCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
