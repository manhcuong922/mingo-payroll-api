using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractAppendix.Queries;

public class GetContractAppendixByIdQueryHandler : IRequestHandler<GetContractAppendixByIdQuery, ContractAppendixDto?>
{
    private readonly IContractAppendixRepository _appendices;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetContractAppendixByIdQueryHandler(
        IContractAppendixRepository appendices,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _appendices = appendices;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ContractAppendixDto?> Handle(GetContractAppendixByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _appendices.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        var hasAll = _currentUser.HasPermission(Permissions.ContractAppendixReadAll);
        var hasSelf = _currentUser.HasPermission(Permissions.ContractAppendixReadSelf);

        if (!hasAll)
        {
            if (!hasSelf)
                throw new ForbiddenAccessException("Caller has neither ContractAppendix.Read.All nor ContractAppendix.Read.Self.");

            var callerId = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Authenticated user context is required.");
            if (entity.Contract.EmployeeInformation?.UserId != callerId)
                throw new ForbiddenAccessException("Caller may only read appendices of their own contracts.");
        }

        return _mapper.Map<ContractAppendixDto>(entity);
    }
}
