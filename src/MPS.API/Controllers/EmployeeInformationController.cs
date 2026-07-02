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
    [HasAnyPermission(
        Permissions.EmployeeInformationReadAll,
        Permissions.EmployeeInformationReadDepartment,
        Permissions.EmployeeInformationReadSelf)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new EmployeeInformationGetAllQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [HasAnyPermission(Permissions.EmployeeInformationReadAll, Permissions.EmployeeInformationReadSelf)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int? positionId,
        [FromQuery] bool? hasAccount,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _mediator.Send(
            new EmployeeInformationGetPagedQuery(keyword, positionId, hasAccount, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasAnyPermission(
        Permissions.EmployeeInformationReadAll,
        Permissions.EmployeeInformationReadDepartment,
        Permissions.EmployeeInformationReadSelf)]
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
            request.FullName,
            request.Dob,
            request.Gender,
            request.CitizenId,
            request.CitizenIssuedDate,
            request.CitizenIssuedPlace,
            request.PermanentAddress,
            request.ContactAddress,
            request.Email,
            request.PhoneNo,
            request.EducationLevel,
            request.PositionId,
            request.DepartmentId,
            request.DependentsNo,
            request.TaxNo,
            request.SocialNo,
            request.BankNumber,
            request.GuardianFullName,
            request.GuardianRelationship,
            request.GuardianPhoneNo,
            request.GuardianAddress);

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
            request.FullName,
            request.Dob,
            request.Gender,
            request.CitizenId,
            request.CitizenIssuedDate,
            request.CitizenIssuedPlace,
            request.PermanentAddress,
            request.ContactAddress,
            request.Email,
            request.PhoneNo,
            request.EducationLevel,
            request.PositionId,
            request.DepartmentId,
            request.DependentsNo,
            request.TaxNo,
            request.SocialNo,
            request.BankNumber,
            request.GuardianFullName,
            request.GuardianRelationship,
            request.GuardianPhoneNo,
            request.GuardianAddress);

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
