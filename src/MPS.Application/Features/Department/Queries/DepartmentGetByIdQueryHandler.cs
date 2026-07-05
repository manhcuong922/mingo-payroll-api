using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Queries;

public class DepartmentGetByIdQueryHandler : IRequestHandler<DepartmentGetByIdQuery, DepartmentDto?>
{
    private readonly IDepartmentRepository _repository;
    private readonly IMapper _mapper;

    public DepartmentGetByIdQueryHandler(IDepartmentRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<DepartmentDto?> Handle(DepartmentGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<DepartmentDto>(entity);
    }
}
