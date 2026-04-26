using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.TypePerformanceMetric.Commands;

public class TypePerformanceMetricDeleteCommandHandler
    : IRequestHandler<TypePerformanceMetricDeleteCommand, bool>
{
    private readonly ITypePerformanceMetricRepository _repository;

    public TypePerformanceMetricDeleteCommandHandler(ITypePerformanceMetricRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(TypePerformanceMetricDeleteCommand request, CancellationToken cancellationToken)
    {
        return await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}
