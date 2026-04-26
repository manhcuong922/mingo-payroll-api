using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.PerformanceMetric.Commands;

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
            Weight = request.Weight,
            TypePerformanceMetricId = request.TypePerformanceMetricId
        };

        return await _repository.AddAsync(entity, request.TargetRoleIds, cancellationToken);
    }
}
