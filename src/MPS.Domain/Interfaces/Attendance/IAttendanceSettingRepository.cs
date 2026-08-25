using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IAttendanceSettingRepository
{
    Task<AttendanceSetting> GetOrCreateDefaultAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc cấu hình hiện có, KHÔNG tạo dòng mới. Dùng cho đường READ (query, báo cáo) — một truy vấn
    /// không được ghi DB. Chưa có dòng nào ⇒ null, caller lùi về hằng số mặc định.
    /// </summary>
    Task<AttendanceSetting?> GetAsync(CancellationToken cancellationToken = default);

    Task<AttendanceSetting> UpdateAsync(AttendanceSetting entity, CancellationToken cancellationToken = default);
}
