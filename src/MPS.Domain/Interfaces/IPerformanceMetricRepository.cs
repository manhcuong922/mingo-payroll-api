using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IPerformanceMetricRepository
{
    Task<PerformanceMetric?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PerformanceMetric>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalWeightByTypeExcludingAsync(int typeId, int? excludeId, CancellationToken cancellationToken = default);
    Task<bool> AllRolesExistAsync(IEnumerable<int> roleIds, CancellationToken cancellationToken = default);
    Task<int> AddAsync(PerformanceMetric entity, IEnumerable<int> targetRoleIds, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PerformanceMetric entity, IEnumerable<int> targetRoleIds, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
