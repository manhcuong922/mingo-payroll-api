using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record CreateWfhRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string WorkPlan,
    string? Reason,
    OvertimeApproverRoute ApproverRoute,
    string? OverrideReason);

public sealed record ApproveWfhRequestBody(
    string? ReviewNote,
    string? OverrideReason);

public sealed record RejectWfhRequestBody(string? ReviewNote);

public sealed record CancelWfhRequestBody(string? CancelReason);
