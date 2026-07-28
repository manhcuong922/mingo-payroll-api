using MPS.Domain.Enums.Attendance;

namespace MPS.Application.DTOs.Attendance;

public sealed record MonthlyScheduleParseResult(
    string? ScheduleName,
    int? Year,
    int? Month,
    string? Notes,
    IReadOnlyList<MonthlyScheduleParsedAssignment> Assignments,
    IReadOnlyList<MonthlyScheduleParseError> Errors);

/// <summary>
/// Assignment thô đọc từ Excel. <see cref="EmployeeCode"/> chứa mã đích của row —
/// theo mode: Employees → EmployeeCode, Departments → DepartmentCode.
/// <see cref="EmployeeName"/> chứa tên hiển thị (Họ tên hoặc Tên phòng ban).
/// </summary>
public sealed record MonthlyScheduleParsedAssignment(
    int RowIndex,
    int ColumnIndex,
    string EmployeeCode,
    string? EmployeeName,
    DateOnly WorkDate,
    string ShiftCode);

public sealed record MonthlyScheduleParseError(
    int? RowIndex,
    int? ColumnIndex,
    string Code,
    string Message);

public sealed record MonthlyScheduleImportPreviewDto(
    string PreviewToken,
    string? ScheduleName,
    int? Year,
    int? Month,
    int TotalCells,
    int ResolvedOperations,
    int SkippedCells,
    IReadOnlyList<MonthlyScheduleImportErrorDto> Errors,
    IReadOnlyList<MonthlyScheduleImportResolvedItemDto> ResolvedItems,
    ScheduleApplyTarget ApplyTarget,
    IReadOnlyList<MonthlyScheduleResolvedDepartmentDto>? ResolvedDepartments);

/// <summary>
/// Chỉ populate cho Departments mode — cho HR biết mỗi row phòng ban đã expand ra
/// bao nhiêu NV active. Null / empty cho Employees mode.
/// </summary>
public sealed record MonthlyScheduleResolvedDepartmentDto(
    int Id,
    string Code,
    string Name,
    int EmployeeCount);

public sealed record MonthlyScheduleImportResolvedItemDto(
    string EmployeeCode,
    string? EmployeeName,
    DateOnly WorkDate,
    string ShiftCode);

public sealed record MonthlyScheduleImportErrorDto(
    int? RowIndex,
    int? ColumnIndex,
    string? EmployeeCode,
    DateOnly? WorkDate,
    string? ShiftCode,
    string Code,
    string Message);

public sealed record MonthlyScheduleImportConfirmResult(
    string BatchName,
    DateOnly FromDate,
    DateOnly ToDate,
    int TemplateCount,
    int Applied,
    int Skipped,
    int Conflicts,
    IReadOnlyList<MonthlyScheduleImportGroupResultDto> Groups);

public sealed record MonthlyScheduleImportGroupResultDto(
    int ScheduleTemplateId,
    string ScheduleTemplateName,
    int ShiftTemplateId,
    string ShiftTemplateCode,
    IReadOnlyList<int> DaysOfMonth,
    int EmployeeCount,
    int Applied,
    int Skipped,
    int Conflicts);
