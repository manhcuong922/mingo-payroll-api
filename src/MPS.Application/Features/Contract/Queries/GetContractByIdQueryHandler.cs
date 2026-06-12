using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Queries;

public class GetContractByIdQueryHandler : IRequestHandler<GetContractByIdQuery, ContractDetailDto?>
{
    private readonly IContractRepository _repository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetContractByIdQueryHandler(
        IContractRepository repository,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ContractDetailDto?> Handle(GetContractByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, includeAllowances: true, cancellationToken);
        if (entity is null) return null;

        var hasAll = _currentUser.HasPermission(Permissions.ContractReadAll);
        var hasSelf = _currentUser.HasPermission(Permissions.ContractReadSelf);

        if (!hasAll)
        {
            if (!hasSelf)
                throw new ForbiddenAccessException("Caller has neither Contract.Read.All nor Contract.Read.Self.");

            var callerId = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Authenticated user context is required.");
            if (entity.EmployeeInformation?.UserId != callerId)
                throw new ForbiddenAccessException("Caller may only read their own contracts.");
        }

        return _mapper.Map<ContractDetailDto>(entity);
    }
}
