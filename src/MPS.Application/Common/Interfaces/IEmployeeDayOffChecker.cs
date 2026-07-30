using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Trả lời câu hỏi "hôm nay nhân viên X có nghỉ không" cho luồng chấm công, theo thứ tự ưu tiên:
/// (1) đơn nghỉ phép đã duyệt (cả có lương lẫn không lương) → (2) ngày lễ → (3) cuối tuần T7/CN.
/// (2) và (3) chỉ tính là nghỉ khi nhân viên KHÔNG được phân ca ngày đó — nhiều công nhân có ca T7
/// và ca ngày lễ được phân chủ ý (<c>ScheduleTemplate.IncludeRestDaysAndHolidays</c>).
///
/// Khác <see cref="IDayTypeResolver"/> (dùng cho tính lương): resolver đó lọc <c>IsPaid == true</c>
/// nên bỏ sót nghỉ không lương, và coi mọi T7/CN là ngày nghỉ bất kể ca làm.
/// </summary>
public interface IEmployeeDayOffChecker
{
    Task<DayOffStatusDto> CheckAsync(
        int employeeInformationId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
