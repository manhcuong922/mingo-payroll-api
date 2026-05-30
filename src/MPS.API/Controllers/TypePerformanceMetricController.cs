using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.TypePerformanceMetric;
using MPS.Application.Features.TypePerformanceMetric.Commands;
using MPS.Application.Features.TypePerformanceMetric.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TypePerformanceMetricController : ControllerBase
{
    private readonly IMediator _mediator;

    public TypePerformanceMetricController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.TypePerformanceMetricReadAll)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TypePerformanceMetricGetAllQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.TypePerformanceMetricReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TypePerformanceMetricGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.TypePerformanceMetricCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTypePerformanceMetricRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(
            new TypePerformanceMetricCreateCommand(request.Name, request.Weight), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.TypePerformanceMetricUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateTypePerformanceMetricRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(
            new TypePerformanceMetricUpdateCommand(id, request.Name, request.Weight), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.TypePerformanceMetricDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(
            new TypePerformanceMetricDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
