using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

/// <summary>
/// Repository Interface cho PerformanceMetric - hợp đồng truy cập dữ liệu (EF Core / SQL Server).
/// </summary>
public interface IPerformanceMetricRepository
{
    Task<PerformanceMetric?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PerformanceMetric>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<int> AddAsync(
        PerformanceMetric entity,
        IEnumerable<int> targetRoleIds,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        PerformanceMetric entity,
        IEnumerable<int> targetRoleIds,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
