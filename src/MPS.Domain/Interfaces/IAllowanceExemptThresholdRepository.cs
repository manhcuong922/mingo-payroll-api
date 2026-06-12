using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface IAllowanceExemptThresholdRepository
{
    Task<AllowanceExemptThresholdVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<AllowanceExemptThresholdVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);
    Task<AllowanceExemptThresholdVersion?> GetActiveForAllowanceTypeAsync(int allowanceTypeId, CancellationToken ct);
    Task<AllowanceExemptThresholdVersion?> GetActiveAtForAllowanceTypeAsync(int allowanceTypeId, DateOnly date, CancellationToken ct);
    Task<int> AddAsync(AllowanceExemptThresholdVersion entity, bool applyImmediately, CancellationToken ct);
    Task<bool> UpdateAsync(AllowanceExemptThresholdVersion entity, CancellationToken ct);
    Task<bool> DeletePendingAsync(int id, CancellationToken ct);
}
