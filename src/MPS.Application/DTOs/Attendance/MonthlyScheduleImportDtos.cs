namespace MPS.Application.DTOs.Attendance;

public sealed record MonthlyScheduleParseResult(
    string? ScheduleName,
    int? Year,
    int? Month,
    string? Notes,
    IReadOnlyList<MonthlyScheduleParsedAssignment> Assignments,
    IReadOnlyList<MonthlyScheduleParseError> Errors);

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
    IReadOnlyList<MonthlyScheduleImportResolvedItemDto> ResolvedItems);

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
