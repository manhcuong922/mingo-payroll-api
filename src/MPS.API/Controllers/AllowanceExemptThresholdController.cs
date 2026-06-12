using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Compliance;
using MPS.Application.Features.Compliance.AllowanceExemptThreshold.Commands;
using MPS.Application.Features.Compliance.AllowanceExemptThreshold.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/compliance/allowance-exempt-thresholds")]
public class AllowanceExemptThresholdController : ControllerBase
{
    private readonly IMediator _mediator;

    public AllowanceExemptThresholdController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.AllowanceExemptThresholdReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllAllowanceExemptThresholdsQuery(includeArchived), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.AllowanceExemptThresholdReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllowanceExemptThresholdByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active")]
    [HasPermission(Permissions.AllowanceExemptThresholdReadAll)]
    public async Task<IActionResult> GetActive(
        [FromQuery] int allowanceTypeId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveAllowanceExemptThresholdQuery(allowanceTypeId), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active-at")]
    [HasPermission(Permissions.AllowanceExemptThresholdReadAll)]
    public async Task<IActionResult> GetActiveAt(
        [FromQuery] int allowanceTypeId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveAllowanceExemptThresholdAtQuery(allowanceTypeId, date), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.AllowanceExemptThresholdCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAllowanceExemptThresholdRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateAllowanceExemptThresholdCommand(
            request.AllowanceTypeId,
            request.ThresholdMode,
            request.MonthlyExemptAmount,
            request.AppliesToTax,
            request.AppliesToSocial,
            request.LegalReference,
            request.Note), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.AllowanceExemptThresholdUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateAllowanceExemptThresholdRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateAllowanceExemptThresholdCommand(
            id,
            request.ThresholdMode,
            request.MonthlyExemptAmount,
            request.AppliesToTax,
            request.AppliesToSocial,
            request.LegalReference,
            request.Note), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.AllowanceExemptThresholdDeleteAll)]
    public async Task<IActionResult> DeletePending(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteAllowanceExemptThresholdPendingCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
