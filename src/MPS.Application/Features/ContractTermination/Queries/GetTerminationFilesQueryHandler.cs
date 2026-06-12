using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Application.Features.Contract.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTermination.Queries;

public class GetTerminationFilesQueryHandler : IRequestHandler<GetTerminationFilesQuery, IReadOnlyList<ContractFileDto>>
{
    private readonly IContractRepository _contracts;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public GetTerminationFilesQueryHandler(
        IContractRepository contracts,
        IFileStorageService storage,
        ICurrentUserService currentUser)
    {
        _contracts = contracts;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ContractFileDto>> Handle(
        GetTerminationFilesQuery request,
        CancellationToken cancellationToken)
    {
        var hasAll = _currentUser.HasPermission(Permissions.ContractFileReadAll);
        var hasSelf = _currentUser.HasPermission(Permissions.ContractFileReadSelf);

        if (!hasAll && !hasSelf)
            throw new ForbiddenAccessException("Caller has neither ContractFile.Read.All nor ContractFile.Read.Self.");

        var contract = await _contracts.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (!hasAll)
        {
            var callerId = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Authenticated user context is required.");
            if (contract.EmployeeInformation?.UserId != callerId)
                throw new ForbiddenAccessException("Caller may only read termination files of their own contracts.");
        }

        var files = await _contracts.GetFilesByTargetAsync(
            FileTargetType.Termination, request.ContractId, cancellationToken);

        return await ContractFileDtoBuilder.BuildAsync(files, _storage, cancellationToken);
    }
}
