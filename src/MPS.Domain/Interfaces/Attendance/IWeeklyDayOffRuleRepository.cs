using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IWeeklyDayOffRuleRepository
{
    Task<IReadOnlyList<WeeklyDayOffRule>> ListAsync(
        bool includeInactive,
        string? applyScope,
        DateOnly? effectiveOn,
        CancellationToken cancellationToken = default);

    Task<WeeklyDayOffRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeeklyDayOffRule>> FindActiveOnDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeeklyDayOffRule>> FindActiveInRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    Task<int> AddAsync(WeeklyDayOffRule entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(WeeklyDayOffRule entity, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
