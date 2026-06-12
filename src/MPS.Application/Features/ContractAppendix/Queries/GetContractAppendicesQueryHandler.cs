using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractAppendix.Queries;

public class GetContractAppendicesQueryHandler : IRequestHandler<GetContractAppendicesQuery, IReadOnlyList<ContractAppendixDto>>
{
    private readonly IContractAppendixRepository _appendices;
    private readonly IContractRepository _contracts;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetContractAppendicesQueryHandler(
        IContractAppendixRepository appendices,
        IContractRepository contracts,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _appendices = appendices;
        _contracts = contracts;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ContractAppendixDto>> Handle(GetContractAppendicesQuery request, CancellationToken cancellationToken)
    {
        var hasAll = _currentUser.HasPermission(Permissions.ContractAppendixReadAll);
        var hasSelf = _currentUser.HasPermission(Permissions.ContractAppendixReadSelf);

        if (!hasAll && !hasSelf)
            throw new ForbiddenAccessException("Caller has neither ContractAppendix.Read.All nor ContractAppendix.Read.Self.");

        // Cần load parent contract để (a) verify existence, (b) self-scope check.
        var contract = await _contracts.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (!hasAll)
        {
            var callerId = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Authenticated user context is required.");
            if (contract.EmployeeInformation?.UserId != callerId)
                throw new ForbiddenAccessException("Caller may only read appendices of their own contracts.");
        }

        var entities = await _appendices.GetByContractIdAsync(request.ContractId, cancellationToken);
        return _mapper.Map<IReadOnlyList<ContractAppendixDto>>(entities);
    }
}
