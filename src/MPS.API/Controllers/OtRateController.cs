using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Compliance;
using MPS.Application.Features.Compliance.OtRate.Commands;
using MPS.Application.Features.Compliance.OtRate.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/compliance/ot-rates")]
public class OtRateController : ControllerBase
{
    private readonly IMediator _mediator;

    public OtRateController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.OtRateReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllOtRatesQuery(includeArchived), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.OtRateReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOtRateByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active")]
    [HasPermission(Permissions.OtRateReadAll)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveOtRateQuery(), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active-at")]
    [HasPermission(Permissions.OtRateReadAll)]
    public async Task<IActionResult> GetActiveAt([FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveOtRateAtQuery(date), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.OtRateCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOtRateRequest request,
        CancellationToken cancellationToken)
    {
        var entries = request.Entries
            .Select(e => new OtRateEntryInput(e.OtType, e.Label, e.Rate))
            .ToList();

        var id = await _mediator.Send(new CreateOtRateCommand(
            request.ValidFrom,
            request.ApplyImmediately,
            entries,
            request.LegalReference,
            request.Note), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.OtRateUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateOtRateRequest request,
        CancellationToken cancellationToken)
    {
        var entries = request.Entries
            .Select(e => new OtRateEntryInput(e.OtType, e.Label, e.Rate))
            .ToList();

        var updated = await _mediator.Send(new UpdateOtRateCommand(
            id,
            request.ValidFrom,
            entries,
            request.LegalReference,
            request.Note), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.OtRateDeleteAll)]
    public async Task<IActionResult> DeletePending(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteOtRatePendingCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
