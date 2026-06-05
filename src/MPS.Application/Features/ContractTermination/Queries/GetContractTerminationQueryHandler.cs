using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTermination.Queries;

public class GetContractTerminationQueryHandler : IRequestHandler<GetContractTerminationQuery, ContractTerminationDto?>
{
    private readonly IContractTerminationRepository _terminations;
    private readonly IContractRepository _contracts;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetContractTerminationQueryHandler(
        IContractTerminationRepository terminations,
        IContractRepository contracts,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _terminations = terminations;
        _contracts = contracts;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ContractTerminationDto?> Handle(GetContractTerminationQuery request, CancellationToken cancellationToken)
    {
        var hasAll = _currentUser.HasPermission(Permissions.ContractTerminationReadAll);
        var hasSelf = _currentUser.HasPermission(Permissions.ContractTerminationReadSelf);

        if (!hasAll && !hasSelf)
            throw new ForbiddenAccessException("Caller has neither ContractTermination.Read.All nor ContractTermination.Read.Self.");

        // Cần load contract để self-scope check + verify tồn tại.
        var contract = await _contracts.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (!hasAll)
        {
            var callerId = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Authenticated user context is required.");
            if (contract.EmployeeId != callerId)
                throw new ForbiddenAccessException("Caller may only read termination of their own contracts.");
        }

        var entity = await _terminations.GetByContractIdAsync(request.ContractId, cancellationToken);
        if (entity is null) return null;
        return _mapper.Map<ContractTerminationDto>(entity);
    }
}
