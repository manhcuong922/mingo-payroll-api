namespace MPS.Application.Common.Interfaces.Attendance;

/// <summary>
/// Parse file .xlsx "Bảng chấm công theo kỳ" (format do <c>AttendanceSheetExcelExporter</c> sinh).
/// Trả về raw parsed data — resolution empCode → empId và validate business rules xảy ra ở Preview handler.
/// </summary>
public interface IAttendanceSheetExcelParser
{
    AttendanceSheetParseResult Parse(Stream stream);
}

/// <summary>
/// Kết quả parse. <see cref="Employees"/> gồm mọi NV bắt gặp; <see cref="Errors"/> là lỗi cấu trúc file
/// (không parse tiếp được). Lỗi per-cell (ký hiệu lạ, NV không tồn tại) build ở Preview handler.
/// </summary>
public sealed record AttendanceSheetParseResult(
    int TotalDays,
    IReadOnlyList<AttendanceSheetParsedEmployeeBlock> Employees,
    IReadOnlyList<AttendanceSheetParseError> Errors);

public sealed record AttendanceSheetParsedEmployeeBlock(
    int TopRow,
    string EmployeeCode,
    string EmployeeName,
    string PositionName,
    IReadOnlyList<AttendanceSheetParsedShiftRow> ShiftRows);

public sealed record AttendanceSheetParsedShiftRow(
    int Row,
    string ShiftLabel,
    IReadOnlyDictionary<int, string> SymbolByDayIndex);

public sealed record AttendanceSheetParseError(
    int? Row,
    int? Column,
    string Code,
    string Message);
