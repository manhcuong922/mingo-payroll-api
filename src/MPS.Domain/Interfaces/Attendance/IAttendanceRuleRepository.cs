using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IAttendanceRuleRepository
{
    Task<(IReadOnlyList<AttendanceRule> Items, int Total)> GetPagedAsync(
        bool? isActive,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<AttendanceRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetShiftTemplateIdsUsingRuleAsync(int ruleId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(AttendanceRule entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(AttendanceRule entity, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
