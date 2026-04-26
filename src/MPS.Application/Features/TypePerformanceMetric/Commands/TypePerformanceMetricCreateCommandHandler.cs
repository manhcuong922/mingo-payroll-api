using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.TypePerformanceMetric.Commands;

public class TypePerformanceMetricCreateCommandHandler
    : IRequestHandler<TypePerformanceMetricCreateCommand, int>
{
    private readonly ITypePerformanceMetricRepository _repository;

    public TypePerformanceMetricCreateCommandHandler(ITypePerformanceMetricRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(TypePerformanceMetricCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.TypePerformanceMetric
        {
            Name = request.Name,
            Weight = request.Weight
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
