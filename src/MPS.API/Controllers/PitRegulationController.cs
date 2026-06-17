using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Compliance;
using MPS.Application.Features.Compliance.PitRegulation.Commands;
using MPS.Application.Features.Compliance.PitRegulation.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/compliance/pit-regulations")]
public class PitRegulationController : ControllerBase
{
    private readonly IMediator _mediator;

    public PitRegulationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.PitRegulationReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllPitRegulationsQuery(includeArchived), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.PitRegulationReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPitRegulationByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active")]
    [HasPermission(Permissions.PitRegulationReadAll)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActivePitRegulationQuery(), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active-at")]
    [HasPermission(Permissions.PitRegulationReadAll)]
    public async Task<IActionResult> GetActiveAt([FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActivePitRegulationAtQuery(date), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.PitRegulationCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePitRegulationRequest request,
        CancellationToken cancellationToken)
    {
        var brackets = request.Brackets
            .Select(b => new PitBracketInput(b.Order, b.LowerBound, b.UpperBound, b.Rate))
            .ToList();

        var id = await _mediator.Send(new CreatePitRegulationCommand(
            request.ValidFrom,
            request.ApplyImmediately,
            request.PersonalDeductionAmount,
            request.DependentDeductionAmount,
            request.ShortTermContractMaxMonths,
            request.ShortTermFlatRatePercent,
            request.ShortTermTaxExemptMonthlyIncome,
            brackets,
            request.LegalReference,
            request.Note,
            request.LegalDocumentIds), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.PitRegulationUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePitRegulationRequest request,
        CancellationToken cancellationToken)
    {
        var brackets = request.Brackets
            .Select(b => new PitBracketInput(b.Order, b.LowerBound, b.UpperBound, b.Rate))
            .ToList();

        var updated = await _mediator.Send(new UpdatePitRegulationCommand(
            id,
            request.ValidFrom,
            request.PersonalDeductionAmount,
            request.DependentDeductionAmount,
            request.ShortTermContractMaxMonths,
            request.ShortTermFlatRatePercent,
            request.ShortTermTaxExemptMonthlyIncome,
            brackets,
            request.LegalReference,
            request.Note,
            request.LegalDocumentIds), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.PitRegulationDeleteAll)]
    public async Task<IActionResult> DeletePending(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeletePitRegulationPendingCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
