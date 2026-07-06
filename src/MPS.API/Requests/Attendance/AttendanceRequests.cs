using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record RecordAttendanceRequest(
    int EmployeeInformationId,
    DateOnly WorkDate,
    int ShiftTemplateId,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason);

public sealed record UpdateAttendanceRequest(
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason);

public sealed record BulkRecordAttendanceRowRequest(
    int EmployeeInformationId,
    int ShiftTemplateId,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason);

public sealed record BulkRecordAttendanceRequest(
    DateOnly WorkDate,
    IReadOnlyList<BulkRecordAttendanceRowRequest> Records);

public sealed record UpdateAttendanceSettingRequest(
    bool IsEditLockEnabled,
    TimeOnly DailyEditCutoffTime,
    int LockAfterDays,
    bool AllowAdminOverride,
    bool RequireOverrideReason);
