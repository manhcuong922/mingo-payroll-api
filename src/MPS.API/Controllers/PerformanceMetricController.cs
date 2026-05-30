using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.PerformanceMetric;
using MPS.Application.Features.PerformanceMetric.Commands;
using MPS.Application.Features.PerformanceMetric.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerformanceMetricController : ControllerBase
{
    private readonly IMediator _mediator;

    public PerformanceMetricController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasAnyPermission(Permissions.PerformanceMetricReadAll, Permissions.PerformanceMetricReadSelf)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PerformanceMetricGetAllQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.PerformanceMetricReadAll, Permissions.PerformanceMetricReadSelf)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PerformanceMetricGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.PerformanceMetricCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePerformanceMetricRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PerformanceMetricCreateCommand(
            request.Name,
            request.Description,
            request.Weight,
            request.TypePerformanceMetricId,
            request.TargetRoleIds);

        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.PerformanceMetricUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePerformanceMetricRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PerformanceMetricUpdateCommand(
            id,
            request.Name,
            request.Description,
            request.Weight,
            request.TypePerformanceMetricId,
            request.TargetRoleIds);

        var updated = await _mediator.Send(command, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.PerformanceMetricDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(
            new PerformanceMetricDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
