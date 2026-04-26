using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;
using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Queries;

/// <summary>
/// Handler xử lý truy vấn danh sách PerformanceMetric.
/// </summary>
public class PerformanceMetricGetAllQueryHandler
    : IRequestHandler<PerformanceMetricGetAllQuery, IReadOnlyList<PerformanceMetricDto>>
{
    private readonly IPerformanceMetricRepository _repository;
    private readonly IMapper _mapper;

    public PerformanceMetricGetAllQueryHandler(IPerformanceMetricRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<PerformanceMetricDto>> Handle(PerformanceMetricGetAllQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<PerformanceMetricDto>>(entities);
    }
}
