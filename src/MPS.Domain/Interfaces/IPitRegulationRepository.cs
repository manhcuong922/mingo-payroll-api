using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface IPitRegulationRepository
{
    Task<PitRegulationVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PitRegulationVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);
    Task<PitRegulationVersion?> GetActiveAsync(CancellationToken ct);
    Task<PitRegulationVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);
    Task<int> AddAsync(PitRegulationVersion entity, bool applyImmediately, CancellationToken ct);
    Task<bool> UpdateAsync(PitRegulationVersion entity, CancellationToken ct);
    Task<bool> DeletePendingAsync(int id, CancellationToken ct);
}
