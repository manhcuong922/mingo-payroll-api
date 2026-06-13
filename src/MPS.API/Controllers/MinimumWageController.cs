using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Compliance;
using MPS.Application.Features.Compliance.MinimumWage.Commands;
using MPS.Application.Features.Compliance.MinimumWage.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/compliance/minimum-wages")]
public class MinimumWageController : ControllerBase
{
    private readonly IMediator _mediator;

    public MinimumWageController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.MinimumWageReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllMinimumWagesQuery(includeArchived), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.MinimumWageReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMinimumWageByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active")]
    [HasPermission(Permissions.MinimumWageReadAll)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveMinimumWageQuery(), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active-at")]
    [HasPermission(Permissions.MinimumWageReadAll)]
    public async Task<IActionResult> GetActiveAt([FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveMinimumWageAtQuery(date), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.MinimumWageCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMinimumWageRequest request,
        CancellationToken cancellationToken)
    {
        var regions = request.Regions
            .Select(r => new MinimumWageRegionInput(r.RegionCode, r.AmountPerMonth))
            .ToList();

        var id = await _mediator.Send(new CreateMinimumWageCommand(
            request.ValidFrom,
            request.ApplyImmediately,
            regions,
            request.LegalReference,
            request.Note), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.MinimumWageUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateMinimumWageRequest request,
        CancellationToken cancellationToken)
    {
        var regions = request.Regions
            .Select(r => new MinimumWageRegionInput(r.RegionCode, r.AmountPerMonth))
            .ToList();

        var updated = await _mediator.Send(new UpdateMinimumWageCommand(
            id,
            request.ValidFrom,
            regions,
            request.LegalReference,
            request.Note), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.MinimumWageDeleteAll)]
    public async Task<IActionResult> DeletePending(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteMinimumWagePendingCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
