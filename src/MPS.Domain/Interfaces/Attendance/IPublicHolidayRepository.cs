using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IPublicHolidayRepository
{
    Task<IReadOnlyList<PublicHoliday>> GetByYearAsync(int year, bool includeInactive, CancellationToken cancellationToken = default);

    Task<PublicHoliday?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> IsHolidayAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<bool> ExistsOverlapAsync(DateOnly startDate, DateOnly endDate, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(PublicHoliday entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(PublicHoliday entity, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
