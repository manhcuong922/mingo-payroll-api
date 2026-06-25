using AutoMapper;
using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Queries;

public class DepartmentGetPagedQueryHandler : IRequestHandler<DepartmentGetPagedQuery, PagedResult<DepartmentDto>>
{
    private readonly IDepartmentRepository _repository;
    private readonly IMapper _mapper;

    public DepartmentGetPagedQueryHandler(IDepartmentRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<DepartmentDto>> Handle(DepartmentGetPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Keyword, request.Page, request.PageSize, cancellationToken);

        return new PagedResult<DepartmentDto>(
            _mapper.Map<IReadOnlyList<DepartmentDto>>(items),
            total,
            request.Page,
            request.PageSize);
    }
}
