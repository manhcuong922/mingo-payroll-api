using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Requests.User;
using MPS.Application.Features.User.Commands;
using MPS.Application.Features.User.Queries;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UserGetAllQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UserGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("by-code/{employeeCode}")]
    public async Task<IActionResult> GetByEmployeeCode(string employeeCode, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UserGetByEmployeeCodeQuery(employeeCode), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UserCreateCommand(
            request.EmployeeCode,
            request.FullName,
            request.RoleId,
            request.Username,
            request.Password,
            request.Email,
            request.Dob,
            request.PhoneNo,
            request.Status);

        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UserUpdateCommand(
            id,
            request.EmployeeCode,
            request.FullName,
            request.RoleId,
            request.Username,
            request.Password,
            request.Email,
            request.Dob,
            request.PhoneNo,
            request.Status);

        var updated = await _mediator.Send(command, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new UserDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
