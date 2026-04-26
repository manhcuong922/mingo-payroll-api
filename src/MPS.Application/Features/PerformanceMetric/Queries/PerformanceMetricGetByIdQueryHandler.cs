using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;
using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Queries;

/// <summary>
/// Handler xử lý truy vấn PerformanceMetric theo Id.
/// </summary>
public class PerformanceMetricGetByIdQueryHandler
    : IRequestHandler<PerformanceMetricGetByIdQuery, PerformanceMetricDto?>
{
    private readonly IPerformanceMetricRepository _repository;
    private readonly IMapper _mapper;

    public PerformanceMetricGetByIdQueryHandler(IPerformanceMetricRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PerformanceMetricDto?> Handle(PerformanceMetricGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<PerformanceMetricDto>(entity);
    }
}
