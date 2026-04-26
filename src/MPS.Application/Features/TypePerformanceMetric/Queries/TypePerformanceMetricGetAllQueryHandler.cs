using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.TypePerformanceMetric.Queries;

public class TypePerformanceMetricGetAllQueryHandler
    : IRequestHandler<TypePerformanceMetricGetAllQuery, IReadOnlyList<TypePerformanceMetricDto>>
{
    private readonly ITypePerformanceMetricRepository _repository;
    private readonly IMapper _mapper;

    public TypePerformanceMetricGetAllQueryHandler(ITypePerformanceMetricRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<TypePerformanceMetricDto>> Handle(
        TypePerformanceMetricGetAllQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<TypePerformanceMetricDto>>(entities);
    }
}
