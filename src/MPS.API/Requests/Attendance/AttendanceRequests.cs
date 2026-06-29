using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record RecordAttendanceRequest(
    int EmployeeInformationId,
    DateOnly WorkDate,
    int ShiftTemplateId,
    TimeOnly? CheckInTime,
    TimeOnly? CheckOutTime,
    AttendanceStatus Status,
    string? Notes,
    bool AcknowledgeWeeklyOvertimeWarnings = false);

public sealed record UpdateAttendanceRequest(
    TimeOnly? CheckInTime,
    TimeOnly? CheckOutTime,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason,
    bool AcknowledgeWeeklyOvertimeWarnings = false);

public sealed record BulkRecordAttendanceRowRequest(
    int EmployeeInformationId,
    int ShiftTemplateId,
    TimeOnly? CheckInTime,
    TimeOnly? CheckOutTime,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason);

public sealed record BulkRecordAttendanceRequest(
    DateOnly WorkDate,
    IReadOnlyList<BulkRecordAttendanceRowRequest> Records,
    bool AcknowledgeWeeklyOvertimeWarnings = false);
