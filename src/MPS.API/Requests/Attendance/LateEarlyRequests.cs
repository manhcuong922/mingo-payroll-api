using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record CreateLateEarlyRequest(
    DateOnly WorkDate,
    LateEarlyKind Kind,
    short Minutes,
    string? Reason,
    OvertimeApproverRoute? ApproverRoute,
    string? OverrideReason);

public sealed record ApproveLateEarlyRequestBody(
    string? ReviewNote,
    string? OverrideReason);

public sealed record RejectLateEarlyRequestBody(string? ReviewNote);

public sealed record CancelLateEarlyRequestBody(string? CancelReason);
