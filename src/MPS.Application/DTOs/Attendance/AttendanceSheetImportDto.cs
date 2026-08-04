namespace MPS.Application.DTOs.Attendance;

/// <summary>
/// Response của endpoint Preview import. FE render 4 tab: Thay đổi / Bỏ qua / Cảnh báo / Lỗi.
/// Diff chỉ chứa ô có change (old ≠ new); ô Excel rỗng → no-op → không vào diff.
/// </summary>
public sealed record AttendanceSheetImportPreviewDto(
    string PreviewId,
    DateOnly FromDate,
    DateOnly ToDate,
    AttendanceSheetImportSummaryDto Summary,
    IReadOnlyList<AttendanceSheetImportDiffDto> Diff,
    IReadOnlyList<AttendanceSheetImportConflictDto> Conflicts,
    IReadOnlyList<AttendanceSheetImportWarningDto> Warnings,
    IReadOnlyList<AttendanceSheetImportErrorDto> Errors);

public sealed record AttendanceSheetImportSummaryDto(
    int Creates,
    int Updates,
    int SkippedLocked,
    int Warnings,
    int Errors);

/// <summary>ChangeType: "create" = record chưa tồn tại; "update" = record tồn tại và khác.</summary>
public sealed record AttendanceSheetImportDiffDto(
    int EmployeeInformationId,
    string EmployeeCode,
    string EmployeeName,
    DateOnly WorkDate,
    int ShiftTemplateId,
    string ShiftCode,
    string? OldSymbol,
    string NewSymbol,
    string ChangeType);

public sealed record AttendanceSheetImportConflictDto(
    int EmployeeInformationId,
    string EmployeeCode,
    DateOnly WorkDate,
    string ShiftCode,
    string Reason);

public sealed record AttendanceSheetImportWarningDto(
    string? EmployeeCode,
    DateOnly? WorkDate,
    string? ShiftCode,
    string Code,
    string Message);

public sealed record AttendanceSheetImportErrorDto(
    int? Row,
    int? Column,
    string? EmployeeCode,
    DateOnly? WorkDate,
    string? ShiftCode,
    string Code,
    string Message);

/// <summary>Response của endpoint Confirm.</summary>
public sealed record AttendanceSheetImportConfirmResultDto(
    int Applied,
    int Skipped,
    IReadOnlyList<AttendanceSheetImportConflictDto> Conflicts,
    IReadOnlyList<AttendanceSheetImportWarningDto> Warnings);
