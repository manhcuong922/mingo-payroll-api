using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Contract.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public class CreateContractCommandHandler : IRequestHandler<CreateContractCommand, int>
{
    private readonly IContractRepository _repository;
    private readonly ContractPreflightChecker _preflight;
    private readonly ICurrentUserService _currentUser;

    public CreateContractCommandHandler(
        IContractRepository repository,
        ContractPreflightChecker preflight,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _preflight = preflight;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to create a contract.");

        var (snapshotAmount, snapshotCurrency) = await _preflight.RunAsync(
            new ContractCheckInput(
                ContractNo: request.ContractNo,
                EmployeeId: request.EmployeeId,
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
                ExcludeContractId: null,
                ForceAllowThirdDefinite: request.ForceAllowThirdDefinite),
            cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var entity = ContractBuilder.FromCreate(request, snapshotAmount, snapshotCurrency, userId, nowUtc);
        var allowances = ContractBuilder.BuildAllowances(request.Allowances, snapshotCurrency, nowUtc);

        return await _repository.AddAsync(entity, allowances, cancellationToken);
    }
}
