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
    private readonly IContractNoGenerator _noGenerator;

    private static readonly int[] RetryDelaysMs = [50, 150, 300];

    public CreateContractCommandHandler(
        IContractRepository repository,
        ContractPreflightChecker preflight,
        ICurrentUserService currentUser,
        IContractNoGenerator noGenerator)
    {
        _repository = repository;
        _preflight = preflight;
        _currentUser = currentUser;
        _noGenerator = noGenerator;
    }

    public async Task<int> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to create a contract.");

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
                ExcludeContractId: null,
                ForceAllowThirdDefinite: request.ForceAllowThirdDefinite),
            cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var year = nowUtc.Year;
        var allowances = ContractBuilder.BuildAllowances(request.Allowances, snapshotCurrency, nowUtc);

        string? lastAttemptedNo = null;
        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(RetryDelaysMs[attempt - 1], cancellationToken);

            var contractNo = await _noGenerator.GenerateAsync(
                request.ContractCategory, request.ContractSubtype, year, cancellationToken);
            lastAttemptedNo = contractNo;

            var entity = ContractBuilder.FromCreate(request, contractNo, snapshotAmount, snapshotCurrency, userId, nowUtc);

            try
            {
                return await _repository.AddAsync(entity, allowances, cancellationToken);
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                if (attempt == RetryDelaysMs.Length)
                    throw new ContractNoCollisionException(contractNo);
            }
        }

        throw new ContractNoCollisionException(lastAttemptedNo!);
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("2601") || msg.Contains("2627") || msg.Contains("UNIQUE");
    }
}
