using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.Application.Features.Employee.Dependent.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/employee-dependents")]
[Authorize]
public class EmployeeDependentController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeeDependentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.EmployeeDependentReadAll)]
    public async Task<IActionResult> GetByEmployee(
        [FromQuery] int employeeInformationId,
        [FromQuery] bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetEmployeeDependentsQuery(employeeInformationId, onlyActive), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.EmployeeDependentReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyDependentsQuery(onlyActive), cancellationToken);
        return Ok(result);
    }
}
