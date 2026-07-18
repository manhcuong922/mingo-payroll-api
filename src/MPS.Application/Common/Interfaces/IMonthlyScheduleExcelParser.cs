using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Parse file Excel "bảng phân ca theo tháng":
///   - Sheet "ScheduleInfo": metadata key/value (Schedule Name, Month=MM/YYYY, Notes).
///   - Sheet "Assignments": header có "Employee Code", "Employee Name", các cột ngày 1..N.
///     Cell ngày = ShiftTemplate.Code, hoặc "OFF" (case-insensitive)/rỗng = bỏ qua.
/// Không truy cập DB; chỉ trả về dữ liệu thô + parse-time errors.
/// </summary>
public interface IMonthlyScheduleExcelParser
{
    MonthlyScheduleParseResult Parse(Stream stream);
}
