using MPS.Domain.Interfaces;
using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Commands;

/// <summary>
/// Handler xử lý cập nhật PerformanceMetric.
/// </summary>
public class PerformanceMetricUpdateCommandHandler
    : IRequestHandler<PerformanceMetricUpdateCommand, bool>
{
    private readonly IPerformanceMetricRepository _repository;

    public PerformanceMetricUpdateCommandHandler(IPerformanceMetricRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(PerformanceMetricUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.PerformanceMetric
        {
            Id = request.Id,
            Name = request.Name,
            Description = request.Description,
            Weight = request.Weight
        };

        return await _repository.UpdateAsync(entity, request.TargetRoleIds, cancellationToken);
    }
}
