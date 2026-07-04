using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IAttendanceSettingRepository
{
    Task<AttendanceSetting> GetOrCreateDefaultAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    Task<AttendanceSetting> UpdateAsync(AttendanceSetting entity, CancellationToken cancellationToken = default);
}
