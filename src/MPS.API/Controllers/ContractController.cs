using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Contract;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize]
public class ContractController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasAnyPermission(Permissions.ContractReadAll, Permissions.ContractReadSelf)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeId,
        [FromQuery] ContractStatus? status,
        [FromQuery] ContractCategory? category,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetContractsQuery(employeeId, status, category, page, size),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.ContractReadAll, Permissions.ContractReadSelf)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContractByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.ContractCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContractRequest request,
        CancellationToken cancellationToken)
    {
        var cmd = new CreateContractCommand(
            request.ContractNo,
            request.ContractCategory,
            request.ContractSubtype,
            request.Form,
            request.EmployeeId,
            request.EmployerPartyId,
            request.EmployerSignerUserId,
            request.PositionId,
            request.JobDescription,
            request.WorkLocation,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.SalaryGradeId,
            request.SalaryFormType,
            request.SalaryPayCycle,
            request.PromotionPolicy,
            request.WorkingTimeJson,
            request.PpePolicy,
            request.SocialInsuranceFlag,
            request.HealthInsuranceFlag,
            request.UnemploymentInsuranceFlag,
            request.TrainingPolicy,
            request.ConfidentialityClause,
            request.ContractText,
            request.TemplateId,
            request.HasProbation,
            request.ProbationStartDate,
            request.ProbationEndDate,
            request.ProbationSalary,
            request.ProbationResultStatus,
            request.ParentContractId,
            (request.Allowances ?? Array.Empty<ContractAllowanceItemRequest>())
                .Select(a => new ContractAllowanceInput(a.AllowanceTypeId, a.Amount, a.Currency, a.Note))
                .ToList(),
            request.ForceAllowThirdDefinite);

        var id = await _mediator.Send(cmd, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ContractUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateContractRequest request,
        CancellationToken cancellationToken)
    {
        var cmd = new UpdateContractCommand(
            id,
            request.ContractNo,
            request.ContractCategory,
            request.ContractSubtype,
            request.Form,
            request.EmployeeId,
            request.EmployerPartyId,
            request.EmployerSignerUserId,
            request.PositionId,
            request.JobDescription,
            request.WorkLocation,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.SalaryGradeId,
            request.SalaryFormType,
            request.SalaryPayCycle,
            request.PromotionPolicy,
            request.WorkingTimeJson,
            request.PpePolicy,
            request.SocialInsuranceFlag,
            request.HealthInsuranceFlag,
            request.UnemploymentInsuranceFlag,
            request.TrainingPolicy,
            request.ConfidentialityClause,
            request.ContractText,
            request.TemplateId,
            request.HasProbation,
            request.ProbationStartDate,
            request.ProbationEndDate,
            request.ProbationSalary,
            request.ProbationResultStatus,
            request.ParentContractId,
            (request.Allowances ?? Array.Empty<ContractAllowanceItemRequest>())
                .Select(a => new ContractAllowanceInput(a.AllowanceTypeId, a.Amount, a.Currency, a.Note))
                .ToList(),
            request.ForceAllowThirdDefinite);

        var updated = await _mediator.Send(cmd, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ContractDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteContractCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.ContractCancelAll)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new CancelContractCommand(id), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    // --- Allowance sub-resource ---

    [HttpPost("{id:int}/allowances")]
    [HasPermission(Permissions.ContractUpdateAll)]
    public async Task<IActionResult> AddAllowance(
        int id,
        [FromBody] AddContractAllowanceRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new AddContractAllowanceCommand(
            id, request.AllowanceTypeId, request.Amount, request.Currency, request.Note), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/allowances/{allowanceTypeId:int}")]
    [HasPermission(Permissions.ContractUpdateAll)]
    public async Task<IActionResult> UpdateAllowance(
        int id,
        int allowanceTypeId,
        [FromBody] UpdateContractAllowanceRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new UpdateContractAllowanceCommand(
            id, allowanceTypeId, request.Amount, request.Currency, request.Note), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}/allowances/{allowanceTypeId:int}")]
    [HasPermission(Permissions.ContractUpdateAll)]
    public async Task<IActionResult> RemoveAllowance(
        int id,
        int allowanceTypeId,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new RemoveContractAllowanceCommand(id, allowanceTypeId), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
