using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record CreateAttendanceAdjustmentRequest(
    int AttendanceRecordId,
    AttendanceStatus NewStatus,
    DateTime? NewCheckInAt,
    DateTime? NewCheckOutAt,
    string Reason);

public sealed record ReviewAttendanceAdjustmentRequest(string? ReviewNote);

public sealed record OverrideAttendanceRequest(
    AttendanceStatus NewStatus,
    DateTime? NewCheckInAt,
    DateTime? NewCheckOutAt,
    string OverrideReason);
