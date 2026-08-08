using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Compliance;
using MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;
using MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/compliance/social-insurance-rates")]
public class SocialInsuranceRateController : ControllerBase
{
    private readonly IMediator _mediator;

    public SocialInsuranceRateController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.SocialInsuranceRateReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllSocialInsuranceRatesQuery(includeArchived), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.SocialInsuranceRateReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSocialInsuranceRateByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active")]
    [HasPermission(Permissions.SocialInsuranceRateReadAll)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveSocialInsuranceRateQuery(), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("active-at")]
    [HasPermission(Permissions.SocialInsuranceRateReadAll)]
    public async Task<IActionResult> GetActiveAt([FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveSocialInsuranceRateAtQuery(date), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.SocialInsuranceRateCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSocialInsuranceRateRequest request,
        CancellationToken cancellationToken)
    {
        var funds = request.Funds
            .Select(f => new FundEntryInput(f.FundCode, f.FundName, f.EmployeePercent, f.EmployerPercent))
            .ToList();

        var id = await _mediator.Send(new CreateSocialInsuranceRateCommand(
            request.ValidFrom,
            request.ApplyImmediately,
            request.EmployeeSocialPercent,
            request.EmployerSocialPercent,
            request.EmployerOccupationalDiseasePercent,
            request.EmployeeHealthPercent,
            request.EmployerHealthPercent,
            request.EmployeeUnemploymentPercent,
            request.EmployerUnemploymentPercent,
            request.EmployerUnionFundPercent,
            request.EmployeeUnionDuesPercent,
            request.EmployeeUnionDuesCapPercent,
            request.ReferenceAmount,
            request.SocialCapMultiplier,
            request.HealthCapMultiplier,
            request.UnemploymentCapMultiplier,
            funds,
            request.EligibilityMode,
            request.EligibleContractSubtypes,
            request.MinTenureMonths,
            request.LegalReference,
            request.Note,
            request.LegalDocumentIds), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.SocialInsuranceRateUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateSocialInsuranceRateRequest request,
        CancellationToken cancellationToken)
    {
        var funds = request.Funds
            .Select(f => new FundEntryInput(f.FundCode, f.FundName, f.EmployeePercent, f.EmployerPercent))
            .ToList();

        var updated = await _mediator.Send(new UpdateSocialInsuranceRateCommand(
            id,
            request.ValidFrom,
            request.EmployeeSocialPercent,
            request.EmployerSocialPercent,
            request.EmployerOccupationalDiseasePercent,
            request.EmployeeHealthPercent,
            request.EmployerHealthPercent,
            request.EmployeeUnemploymentPercent,
            request.EmployerUnemploymentPercent,
            request.EmployerUnionFundPercent,
            request.EmployeeUnionDuesPercent,
            request.EmployeeUnionDuesCapPercent,
            request.ReferenceAmount,
            request.SocialCapMultiplier,
            request.HealthCapMultiplier,
            request.UnemploymentCapMultiplier,
            funds,
            request.EligibilityMode,
            request.EligibleContractSubtypes,
            request.MinTenureMonths,
            request.LegalReference,
            request.Note,
            request.LegalDocumentIds), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.SocialInsuranceRateDeleteAll)]
    public async Task<IActionResult> DeletePending(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteSocialInsuranceRatePendingCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
