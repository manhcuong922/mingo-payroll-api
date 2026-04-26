using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.TypePerformanceMetric.Commands;

public class TypePerformanceMetricUpdateCommandHandler
    : IRequestHandler<TypePerformanceMetricUpdateCommand, bool>
{
    private readonly ITypePerformanceMetricRepository _repository;

    public TypePerformanceMetricUpdateCommandHandler(ITypePerformanceMetricRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(TypePerformanceMetricUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.TypePerformanceMetric
        {
            Id = request.Id,
            Name = request.Name,
            Weight = request.Weight
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
