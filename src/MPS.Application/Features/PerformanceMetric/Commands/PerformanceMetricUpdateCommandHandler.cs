using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.PerformanceMetric.Commands;

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
            Weight = request.Weight,
            TypePerformanceMetricId = request.TypePerformanceMetricId
        };

        return await _repository.UpdateAsync(entity, request.TargetRoleIds, cancellationToken);
    }
}
