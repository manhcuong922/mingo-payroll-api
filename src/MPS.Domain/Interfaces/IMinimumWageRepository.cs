using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface IMinimumWageRepository
{
    Task<MinimumWageVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<MinimumWageVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);
    Task<MinimumWageVersion?> GetActiveAsync(CancellationToken ct);
    Task<MinimumWageVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);
    Task<int> AddAsync(MinimumWageVersion entity, bool applyImmediately, CancellationToken ct);
    Task<bool> UpdateAsync(MinimumWageVersion entity, CancellationToken ct);
    Task<bool> DeletePendingAsync(int id, CancellationToken ct);
}
