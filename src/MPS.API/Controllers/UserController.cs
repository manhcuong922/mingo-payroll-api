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

    [HttpGet("{employeeCode}")]
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

        var employeeCode = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetByEmployeeCode), new { employeeCode }, new { employeeCode });
    }

    [HttpPut("{employeeCode}")]
    public async Task<IActionResult> Update(
        string employeeCode,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UserUpdateCommand(
            employeeCode,
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

    [HttpDelete("{employeeCode}")]
    public async Task<IActionResult> Delete(string employeeCode, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new UserDeleteCommand(employeeCode), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}