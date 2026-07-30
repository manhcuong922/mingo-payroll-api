using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Parse file Excel "bảng phân ca theo tháng":
///   - Metadata key/value (Schedule Name, Month=MM/YYYY, Notes).
///   - Header row có cột code (Mã NV hoặc Mã phòng ban theo <paramref name="applyTarget"/>),
///     Họ tên/Tên phòng ban optional, và các cột ngày 1..N.
///     Cell ngày = ShiftTemplate.Code, hoặc "OFF" (case-insensitive)/rỗng = bỏ qua.
///
/// Field <see cref="MonthlyScheduleParsedAssignment.EmployeeCode"/> mang nghĩa "target code":
///   - Employees mode → EmployeeCode.
///   - Departments mode → DepartmentCode (chưa resolve sang NV).
///
/// Không truy cập DB; chỉ trả về dữ liệu thô + parse-time errors.
/// </summary>
public interface IMonthlyScheduleExcelParser
{
    MonthlyScheduleParseResult Parse(
        Stream stream,
        ScheduleApplyTarget applyTarget = ScheduleApplyTarget.Employees);
}
