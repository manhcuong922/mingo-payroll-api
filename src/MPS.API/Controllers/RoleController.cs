using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Role;
using MPS.Application.Features.Role.Commands;
using MPS.Application.Features.Role.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RoleController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoleController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.RoleReadAll)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RoleGetAllQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.RoleReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RoleGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.RoleCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RoleCreateCommand(
            request.Name,
            request.Description,
            request.PermissionIds ?? Array.Empty<int>());

        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.RoleUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RoleUpdateCommand(id, request.Name, request.Description);
        var updated = await _mediator.Send(command, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.RoleDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new RoleDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/permissions")]
    [HasPermission(Permissions.RoleManagePermissionsAll)]
    public async Task<IActionResult> UpdatePermissions(
        int id,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRolePermissionsCommand(
            id,
            request.PermissionIds ?? Array.Empty<int>());

        var updated = await _mediator.Send(command, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }
}
