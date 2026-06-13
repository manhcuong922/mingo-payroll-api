using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.AllowanceType;
using MPS.Application.Features.AllowanceType.Commands;
using MPS.Application.Features.AllowanceType.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/allowance-types")]
public class AllowanceTypeController : ControllerBase
{
    private readonly IMediator _mediator;

    public AllowanceTypeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.AllowanceTypeReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AllowanceTypeGetAllQuery(isActive), cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [HasPermission(Permissions.AllowanceTypeReadAll)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _mediator.Send(
            new AllowanceTypeGetPagedQuery(isActive, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.AllowanceTypeReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AllowanceTypeGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.AllowanceTypeCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAllowanceTypeRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new AllowanceTypeCreateCommand(
            request.Code,
            request.Name,
            request.Description,
            request.DefaultAmount,
            request.IsBhxhBase,
            request.IsTaxable,
            request.IsActive), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.AllowanceTypeUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateAllowanceTypeRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new AllowanceTypeUpdateCommand(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.DefaultAmount,
            request.IsBhxhBase,
            request.IsTaxable,
            request.IsActive), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.AllowanceTypeDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new AllowanceTypeDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
