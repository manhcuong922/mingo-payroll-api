using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Queries;

public class DepartmentGetAllQueryHandler : IRequestHandler<DepartmentGetAllQuery, IReadOnlyList<DepartmentDto>>
{
    private readonly IDepartmentRepository _repository;
    private readonly IMapper _mapper;

    public DepartmentGetAllQueryHandler(IDepartmentRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<DepartmentDto>> Handle(DepartmentGetAllQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(request.IncludeInactive, request.Keyword, cancellationToken);
        return _mapper.Map<IReadOnlyList<DepartmentDto>>(entities);
    }
}
