using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Department;
using MPS.Application.Features.Department.Commands;
using MPS.Application.Features.Department.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartmentController : ControllerBase
{
    private readonly IMediator _mediator;

    public DepartmentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.DepartmentReadAll)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DepartmentGetAllQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [HasPermission(Permissions.DepartmentReadAll)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new DepartmentGetPagedQuery(keyword, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.DepartmentReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DepartmentGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.DepartmentCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DepartmentCreateCommand(
            request.Code,
            request.Name,
            request.Description,
            request.ManagerId);

        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.DepartmentUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DepartmentUpdateCommand(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.ManagerId);

        var updated = await _mediator.Send(command, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.DepartmentDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DepartmentDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
