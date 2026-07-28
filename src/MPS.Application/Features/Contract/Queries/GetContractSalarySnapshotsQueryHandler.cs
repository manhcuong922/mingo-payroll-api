using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Queries;

public class GetContractSalarySnapshotsQueryHandler
    : IRequestHandler<GetContractSalarySnapshotsQuery, IReadOnlyList<ContractSalarySnapshotDto>>
{
    private readonly IContractRepository _contracts;
    private readonly IContractAppendixRepository _appendices;
    private readonly ICurrentUserService _currentUser;

    public GetContractSalarySnapshotsQueryHandler(
        IContractRepository contracts,
        IContractAppendixRepository appendices,
        ICurrentUserService currentUser)
    {
        _contracts = contracts;
        _appendices = appendices;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ContractSalarySnapshotDto>> Handle(
        GetContractSalarySnapshotsQuery request,
        CancellationToken cancellationToken)
    {
        var hasAll = _currentUser.HasPermission(Permissions.ContractReadAll);
        var hasSelf = _currentUser.HasPermission(Permissions.ContractReadSelf);
        if (!hasAll && !hasSelf)
            throw new ForbiddenAccessException("Caller has neither Contract.Read.All nor Contract.Read.Self.");

        var contract = await _contracts.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (!hasAll)
        {
            var callerId = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Authenticated user context is required.");
            if (contract.EmployeeInformation?.UserId != callerId)
                throw new ForbiddenAccessException("Caller may only read their own contracts.");
        }

        var snapshots = new List<ContractSalarySnapshotDto>
        {
            new(contract.EffectiveFrom, contract.SalaryAmountSnapshot, contract.SalaryCurrencySnapshot, null, null),
        };

        // Chỉ phụ lục ĐÃ KÍCH HOẠT mới là mốc lương thật — phụ lục nháp chưa ký thì chưa có hiệu lực.
        var appendices = await _appendices.GetByContractIdAsync(request.ContractId, cancellationToken);
        snapshots.AddRange(appendices
            .Where(a => a.Status == AppendixStatus.Active && a.SalaryAmountSnapshot.HasValue)
            .Select(a => new ContractSalarySnapshotDto(
                a.EffectiveFrom,
                a.SalaryAmountSnapshot!.Value,
                a.SalaryCurrencySnapshot ?? contract.SalaryCurrencySnapshot,
                a.Id,
                a.AppendixNo)));

        return snapshots.OrderBy(s => s.EffectiveFrom).ThenBy(s => s.AppendixId ?? 0).ToList();
    }
}
