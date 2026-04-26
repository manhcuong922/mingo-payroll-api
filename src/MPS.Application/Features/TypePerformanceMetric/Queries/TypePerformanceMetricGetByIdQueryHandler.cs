using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.TypePerformanceMetric.Queries;

public class TypePerformanceMetricGetByIdQueryHandler
    : IRequestHandler<TypePerformanceMetricGetByIdQuery, TypePerformanceMetricDto?>
{
    private readonly ITypePerformanceMetricRepository _repository;
    private readonly IMapper _mapper;

    public TypePerformanceMetricGetByIdQueryHandler(ITypePerformanceMetricRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<TypePerformanceMetricDto?> Handle(
        TypePerformanceMetricGetByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<TypePerformanceMetricDto>(entity);
    }
}
