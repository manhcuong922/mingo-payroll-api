using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.EmployeeInformation;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Application.Features.EmployeeInformation.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeInformationController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeeInformationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.EmployeeInformationReadAll)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new EmployeeInformationGetAllQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.EmployeeInformationReadAll, Permissions.EmployeeInformationReadSelf)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new EmployeeInformationGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.EmployeeInformationCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmployeeInformationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new EmployeeInformationCreateCommand(
            request.UserId,
            request.TaxNo,
            request.SocialNo,
            request.BankNumber,
            request.PositionId,
            request.DependentsNo,
            request.CitizenId,
            request.Address);

        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasAnyPermission(Permissions.EmployeeInformationUpdateAll, Permissions.EmployeeInformationUpdateSelf)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateEmployeeInformationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new EmployeeInformationUpdateCommand(
            id,
            request.UserId,
            request.TaxNo,
            request.SocialNo,
            request.BankNumber,
            request.PositionId,
            request.DependentsNo,
            request.CitizenId,
            request.Address);

        var updated = await _mediator.Send(command, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.EmployeeInformationDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new EmployeeInformationDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
