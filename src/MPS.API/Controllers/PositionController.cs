using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Position;
using MPS.Application.Features.Position.Commands;
using MPS.Application.Features.Position.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PositionController : ControllerBase
{
    private readonly IMediator _mediator;

    public PositionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.PositionReadAll)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PositionGetAllQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.PositionReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PositionGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.PositionCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePositionRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new PositionCreateCommand(request.Code, request.Name, request.Description), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.PositionUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePositionRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new PositionUpdateCommand(id, request.Code, request.Name, request.Description), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.PositionDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new PositionDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
