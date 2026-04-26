using MPS.Domain.Interfaces;
using MediatR;

namespace MPS.Application.Features.PerformanceMetric.Commands;

/// <summary>
/// Handler xử lý xóa PerformanceMetric.
/// </summary>
public class PerformanceMetricDeleteCommandHandler
    : IRequestHandler<PerformanceMetricDeleteCommand, bool>
{
    private readonly IPerformanceMetricRepository _repository;

    public PerformanceMetricDeleteCommandHandler(IPerformanceMetricRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(PerformanceMetricDeleteCommand request, CancellationToken cancellationToken)
    {
        return await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}
