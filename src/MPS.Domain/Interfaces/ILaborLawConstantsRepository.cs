using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface ILaborLawConstantsRepository
{
    Task<LaborLawConstantsVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<LaborLawConstantsVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);
    Task<LaborLawConstantsVersion?> GetActiveAsync(CancellationToken ct);
    Task<LaborLawConstantsVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);
    Task<int> AddAsync(LaborLawConstantsVersion entity, bool applyImmediately, CancellationToken ct);
    Task<bool> UpdateAsync(LaborLawConstantsVersion entity, CancellationToken ct);
    Task<bool> DeletePendingAsync(int id, CancellationToken ct);
}
