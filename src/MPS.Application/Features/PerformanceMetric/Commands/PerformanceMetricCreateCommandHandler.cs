using MPS.Domain.Interfaces;
using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Commands;

/// <summary>
/// Handler xử lý tạo mới PerformanceMetric.
/// </summary>
public class PerformanceMetricCreateCommandHandler
    : IRequestHandler<PerformanceMetricCreateCommand, int>
{
    private readonly IPerformanceMetricRepository _repository;

    public PerformanceMetricCreateCommandHandler(IPerformanceMetricRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(PerformanceMetricCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.PerformanceMetric
        {
            Name = request.Name,
            Description = request.Description,
            Weight = request.Weight
        };

        return await _repository.AddAsync(entity, request.TargetRoleIds, cancellationToken);
    }
}
