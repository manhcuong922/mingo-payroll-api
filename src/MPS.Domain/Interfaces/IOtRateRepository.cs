using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface IOtRateRepository
{
    Task<OtRateVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<OtRateVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);
    Task<OtRateVersion?> GetActiveAsync(CancellationToken ct);
    Task<OtRateVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);
    Task<int> AddAsync(OtRateVersion entity, bool applyImmediately, CancellationToken ct);
    Task<bool> UpdateAsync(OtRateVersion entity, CancellationToken ct);
    Task<bool> DeletePendingAsync(int id, CancellationToken ct);
}
