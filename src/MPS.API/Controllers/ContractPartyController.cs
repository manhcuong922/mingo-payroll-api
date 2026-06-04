using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.ContractParty;
using MPS.Application.Features.ContractParty.Commands;
using MPS.Application.Features.ContractParty.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/contract-parties")]
public class ContractPartyController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractPartyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.ContractPartyReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ContractPartyGetAllQuery(activeOnly), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.ContractPartyReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ContractPartyGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.ContractPartyCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContractPartyRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new ContractPartyCreateCommand(
            request.LegalName,
            request.TaxCode,
            request.Address,
            request.RepresentativeFullName,
            request.RepresentativeTitle), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }
}
