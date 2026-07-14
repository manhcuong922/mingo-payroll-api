using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

/// <summary>
/// Repository cho AttendanceRuleVersion — lookup theo (ruleId, workDate) để tính hệ số
/// point-in-time; add/close version qua flow update rule.
/// </summary>
public interface IAttendanceRuleVersionRepository
{
    /// <summary>Trả version áp dụng cho workDate của rule cụ thể (null nếu không match).</summary>
    Task<AttendanceRuleVersion?> GetAtAsync(int ruleId, DateOnly workDate, CancellationToken cancellationToken = default);

    /// <summary>Trả version đang mở (EffectiveTo = null) của rule cụ thể.</summary>
    Task<AttendanceRuleVersion?> GetOpenAsync(int ruleId, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup: {ruleId → open version}. Rule không có open version sẽ không xuất hiện trong dict.</summary>
    Task<IReadOnlyDictionary<int, AttendanceRuleVersion>> GetOpenByRuleIdsAsync(
        IReadOnlyCollection<int> ruleIds, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup toàn bộ history: {ruleId → list versions} sắp xếp theo EffectiveFrom.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<AttendanceRuleVersion>>> GetByRuleIdsAsync(
        IReadOnlyCollection<int> ruleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert version mới. Ném DomainValidationException nếu overlap với version hiện có của cùng rule.
    /// </summary>
    Task<int> AddAsync(AttendanceRuleVersion entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đóng version đang mở của rule (set EffectiveTo = effectiveTo). No-op nếu không có version mở.
    /// </summary>
    Task CloseOpenVersionAsync(int ruleId, DateOnly effectiveTo, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
