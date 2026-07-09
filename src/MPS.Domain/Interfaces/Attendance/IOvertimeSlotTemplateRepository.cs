using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IOvertimeSlotTemplateRepository
{
    Task<(IReadOnlyList<OvertimeSlotTemplate> Items, int Total)> GetPagedAsync(
        bool? isActive,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<OvertimeSlotTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả về mọi slot active. FE dùng để populate dropdown khi employee đăng ký OT;
    /// slot áp cho mọi loại ngày, hệ số phân biệt qua 7 field multiplier trên entity.
    /// </summary>
    Task<IReadOnlyList<OvertimeSlotTemplate>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(OvertimeSlotTemplate entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(OvertimeSlotTemplate entity, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm và inactive tất cả slot ACTIVE có khung giờ giao thoa với [startTime, endTime].
    /// excludeId để bỏ qua chính slot đang update (không tự inactive mình).
    /// Trả về Code của các slot bị inactive để handler báo cho user biết.
    /// </summary>
    Task<IReadOnlyList<string>> DeactivateOverlappingActiveAsync(
        TimeOnly startTime,
        TimeOnly endTime,
        int? excludeId,
        int updatedByUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Có bất kỳ OvertimeRequest nào (bất kể status) đang reference slot này không?
    /// Dùng cho hard-delete vs soft-deactivate decision.
    /// </summary>
    Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken = default);
}
