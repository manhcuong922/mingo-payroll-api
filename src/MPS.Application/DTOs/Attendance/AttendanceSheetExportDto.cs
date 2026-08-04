namespace MPS.Application.DTOs.Attendance;

/// <summary>
/// DTO cho "Bảng chấm công theo kỳ" — layout matrix NV × ngày × ca (chuẩn form VN).
/// Nguồn: UNION EmployeeShiftAssignment ∪ AttendanceRecord distinct ShiftTemplateId trong range.
/// </summary>
public sealed record AttendanceSheetExportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<AttendanceSheetEmployeeDto> Employees);

/// <summary>
/// 1 nhân viên trong bảng. <c>ShiftRows</c> là danh sách ca đã phân/đã chấm trong kỳ,
/// sort theo <c>ShiftTemplate.StartTime</c>. Rỗng → NV chưa phân ca (render placeholder ở export).
/// </summary>
public sealed record AttendanceSheetEmployeeDto(
    int EmployeeInformationId,
    string EmployeeCode,
    string FullName,
    string PositionName,
    string? DepartmentName,
    IReadOnlyList<AttendanceSheetShiftRowDto> ShiftRows,
    IReadOnlyDictionary<int, decimal> OvertimeHoursByDay);

/// <summary>
/// 1 dòng ca cho 1 NV. <c>SymbolByDay</c> key = day-of-month (1..31), value = "X"/"P"/"V"/"M"/"S"/"1/2"/"L".
/// Ngày không có record → key vắng mặt (render ô rỗng).
/// </summary>
public sealed record AttendanceSheetShiftRowDto(
    int ShiftTemplateId,
    string ShiftCode,
    string ShiftName,
    IReadOnlyDictionary<int, string> SymbolByDay);
