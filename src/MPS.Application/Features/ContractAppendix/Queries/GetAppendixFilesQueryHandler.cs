using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Application.Features.Contract.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractAppendix.Queries;

public class GetAppendixFilesQueryHandler : IRequestHandler<GetAppendixFilesQuery, IReadOnlyList<ContractFileDto>>
{
    private readonly IContractAppendixRepository _appendices;
    private readonly IContractRepository _contracts;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public GetAppendixFilesQueryHandler(
        IContractAppendixRepository appendices,
        IContractRepository contracts,
        IFileStorageService storage,
        ICurrentUserService currentUser)
    {
        _appendices = appendices;
        _contracts = contracts;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ContractFileDto>> Handle(
        GetAppendixFilesQuery request,
        CancellationToken cancellationToken)
    {
        var hasAll = _currentUser.HasPermission(Permissions.ContractFileReadAll);
        var hasSelf = _currentUser.HasPermission(Permissions.ContractFileReadSelf);

        if (!hasAll && !hasSelf)
            throw new ForbiddenAccessException("Caller has neither ContractFile.Read.All nor ContractFile.Read.Self.");

        // Load appendix → resolve ContractId → self-scope check qua contract.EmployeeId.
        var appendix = await _appendices.GetByIdAsync(request.AppendixId, cancellationToken)
            ?? throw new DomainValidationException($"Appendix {request.AppendixId} not found.");

        if (!hasAll)
        {
            var callerId = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Authenticated user context is required.");
            if (appendix.Contract.EmployeeId != callerId)
                throw new ForbiddenAccessException("Caller may only read files of appendices on their own contracts.");
        }

        var files = await _contracts.GetFilesByTargetAsync(
            FileTargetType.Appendix, request.AppendixId, cancellationToken);

        return await ContractFileDtoBuilder.BuildAsync(files, _storage, cancellationToken);
    }
}
