using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface ISocialInsuranceRateRepository
{
    Task<SocialInsuranceRateVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<SocialInsuranceRateVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);
    Task<SocialInsuranceRateVersion?> GetActiveAsync(CancellationToken ct);
    Task<SocialInsuranceRateVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);
    Task<int> AddAsync(SocialInsuranceRateVersion entity, bool applyImmediately, CancellationToken ct);
    Task<bool> UpdateAsync(SocialInsuranceRateVersion entity, CancellationToken ct);
    Task<bool> DeletePendingAsync(int id, CancellationToken ct);
}
