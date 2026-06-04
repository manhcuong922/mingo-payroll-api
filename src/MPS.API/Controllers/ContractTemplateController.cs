using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.ContractTemplate;
using MPS.Application.Features.ContractTemplate.Commands;
using MPS.Application.Features.ContractTemplate.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/contract-templates")]
public class ContractTemplateController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractTemplateController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.ContractTemplateReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] ContractCategory? category,
        [FromQuery] ContractSubtype? subtype,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ContractTemplateGetAllQuery(category, subtype, isActive), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.ContractTemplateReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ContractTemplateGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.ContractTemplateCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContractTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new ContractTemplateCreateCommand(
            request.Name,
            request.ContractCategory,
            request.ContractSubtype,
            request.TemplateBody,
            request.IsActive), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ContractTemplateUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateContractTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new ContractTemplateUpdateCommand(
            id,
            request.Name,
            request.ContractCategory,
            request.ContractSubtype,
            request.TemplateBody,
            request.IsActive), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ContractTemplateDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new ContractTemplateDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
