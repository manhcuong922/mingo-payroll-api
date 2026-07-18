using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Queries;

public class GetContractsQueryHandler : IRequestHandler<GetContractsQuery, PagedResult<ContractSummaryDto>>
{
    private readonly IContractRepository _repository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly IContractLookupReader _lookup;

    public GetContractsQueryHandler(
        IContractRepository repository,
        IMapper mapper,
        ICurrentUserService currentUser,
        IContractLookupReader lookup)
    {
        _repository = repository;
        _mapper = mapper;
        _currentUser = currentUser;
        _lookup = lookup;
    }

    public async Task<PagedResult<ContractSummaryDto>> Handle(GetContractsQuery request, CancellationToken cancellationToken)
    {
        var hasAll = _currentUser.HasPermission(Permissions.ContractReadAll);
        var hasSelf = _currentUser.HasPermission(Permissions.ContractReadSelf);

        if (!hasAll && !hasSelf)
            throw new ForbiddenAccessException("Caller has neither Contract.Read.All nor Contract.Read.Self.");

        // Self-scope: silently filter list to caller's own contracts (do NOT 403).
        var effectiveEmployeeInformationId = request.EmployeeInformationId;
        if (!hasAll && hasSelf)
        {
            var callerId = _currentUser.UserId
                ?? throw new ForbiddenAccessException("Authenticated user context is required.");
            effectiveEmployeeInformationId = await _lookup.GetEmployeeInformationIdByUserIdAsync(callerId, cancellationToken);
        }

        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(
            effectiveEmployeeInformationId,
            request.Status,
            request.Category,
            request.Kind,
            page,
            size,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<ContractSummaryDto>>(items);
        return new PagedResult<ContractSummaryDto>(dtos, total, page, size);
    }
}
