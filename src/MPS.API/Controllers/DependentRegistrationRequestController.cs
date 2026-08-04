using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Employee;
using MPS.Application.Features.Employee.Dependent.Commands;
using MPS.Application.Features.Employee.Dependent.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Employee;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/dependent-requests")]
[Authorize]
public class DependentRegistrationRequestController : ControllerBase
{
    private readonly IMediator _mediator;

    public DependentRegistrationRequestController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.DependentRequestReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeInformationId,
        [FromQuery] DependentRequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetDependentRegistrationRequestsQuery(
            employeeInformationId, status, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.DependentRequestReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] DependentRequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyDependentRegistrationRequestsQuery(status, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.DependentRequestReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDependentRegistrationRequestByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.DependentRequestCreateSelf)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDependentRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateDependentRegistrationRequestCommand(
            request.RequestType,
            request.TargetDependentId,
            request.FullName,
            request.DateOfBirth,
            request.Gender,
            request.Relationship,
            request.TaxIdentificationNumber,
            request.CitizenId,
            request.RegisteredFromDate,
            request.RegisteredToDate,
            request.IdentityDocumentType,
            request.IdentityDocumentNumber,
            request.IdentityDocumentIssuedDate,
            request.Reason), cancellationToken);
        return Created($"/api/dependent-requests/{id}", new { id });
    }

    [HttpPut("{id:int}/approve")]
    [HasPermission(Permissions.DependentRequestApproveAll)]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] ApproveDependentRegistrationBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new ApproveDependentRegistrationRequestCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/reject")]
    [HasPermission(Permissions.DependentRequestApproveAll)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] RejectDependentRegistrationBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new RejectDependentRegistrationRequestCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/cancel")]
    [HasPermission(Permissions.DependentRequestCancelSelf)]
    public async Task<IActionResult> Cancel(
        int id,
        [FromBody] CancelDependentRegistrationBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new CancelDependentRegistrationRequestCommand(id, request.CancelReason), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
