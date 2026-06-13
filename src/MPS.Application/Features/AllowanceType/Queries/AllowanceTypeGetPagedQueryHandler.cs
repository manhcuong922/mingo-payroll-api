using AutoMapper;
using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AllowanceType.Queries;

public class AllowanceTypeGetPagedQueryHandler
    : IRequestHandler<AllowanceTypeGetPagedQuery, PagedResult<AllowanceTypeDto>>
{
    private readonly IAllowanceTypeRepository _repository;
    private readonly IMapper _mapper;

    public AllowanceTypeGetPagedQueryHandler(IAllowanceTypeRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<AllowanceTypeDto>> Handle(
        AllowanceTypeGetPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.IsActive, request.Page, request.PageSize, cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<AllowanceTypeDto>>(items);
        return new PagedResult<AllowanceTypeDto>(dtos, total, request.Page, request.PageSize);
    }
}
