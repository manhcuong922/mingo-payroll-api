using MediatR;
using MPS.Application.Features.Contract.Common;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public class UpdateContractCommandHandler : IRequestHandler<UpdateContractCommand, bool>
{
    private readonly IContractRepository _repository;
    private readonly ContractPreflightChecker _preflight;

    public UpdateContractCommandHandler(IContractRepository repository, ContractPreflightChecker preflight)
    {
        _repository = repository;
        _preflight = preflight;
    }

    public async Task<bool> Handle(UpdateContractCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, includeAllowances: false, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot update contract {request.Id}: status is {existing.Status}, only Draft can be modified.");

        var (snapshotAmount, snapshotCurrency) = await _preflight.RunAsync(
            new ContractCheckInput(
                EmployeeInformationId: request.EmployeeInformationId,
                EmployerPartyId: request.EmployerPartyId,
                EmployerSignerUserId: request.EmployerSignerUserId,
                PositionId: request.PositionId,
                SalaryGradeId: request.SalaryGradeId,
                EffectiveFrom: request.EffectiveFrom,
                ContractCategory: request.ContractCategory,
                ContractSubtype: request.ContractSubtype,
                HasProbation: request.HasProbation,
                ProbationSalary: request.ProbationSalary,
                AllowanceTypeIds: request.Allowances.Select(a => a.AllowanceTypeId).ToList(),
                ExcludeContractId: request.Id,
                ForceAllowThirdDefinite: request.ForceAllowThirdDefinite),
            cancellationToken);

        var nowUtc = DateTime.UtcNow;
        ContractBuilder.ApplyUpdate(existing, request, snapshotAmount, snapshotCurrency, nowUtc);
        var allowances = ContractBuilder.BuildAllowances(request.Allowances, snapshotCurrency, nowUtc);

        return await _repository.UpdateDraftAsync(existing, allowances, cancellationToken);
    }
}
