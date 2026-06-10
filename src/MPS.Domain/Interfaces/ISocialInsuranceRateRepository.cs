using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface ISocialInsuranceRateRepository
{
    Task<SocialInsuranceRateVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<SocialInsuranceRateVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);
    Task<SocialInsuranceRateVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);

    /// <summary>
    /// Thêm version mới trong transaction Serializable. Auto-close record đang open hiện tại
    /// bằng cách set ValidTo = entity.ValidFrom.AddDays(-1).
    /// </summary>
    Task<int> AddAsync(SocialInsuranceRateVersion entity, CancellationToken ct);

    /// <summary>
    /// Chỉ cho phép update record future-dated (ValidFrom > today) và chưa archived.
    /// </summary>
    Task<bool> UpdateAsync(SocialInsuranceRateVersion entity, CancellationToken ct);

    /// <summary>
    /// Soft delete: set IsArchived = true. Phục hồi record liền trước thành open nếu có.
    /// </summary>
    Task<bool> ArchiveAsync(int id, CancellationToken ct);
}
