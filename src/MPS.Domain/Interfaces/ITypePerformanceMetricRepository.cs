using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface ITypePerformanceMetricRepository
{
    Task<TypePerformanceMetric?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TypePerformanceMetric>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalWeightExcludingAsync(int? excludeId, CancellationToken cancellationToken = default);
    Task<bool> HasPerformanceMetricsAsync(int id, CancellationToken cancellationToken = default);
    Task<int> AddAsync(TypePerformanceMetric entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(TypePerformanceMetric entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
