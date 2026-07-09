using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record CreateBatchOvertimeRequest(
    int ProjectId,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    IReadOnlyList<int> EmployeeInformationIds,
    OvertimeApproverRoute ApproverRoute,
    string? Reason);

public sealed record ReviewOvertimeRequest(string? ReviewNote);

public sealed record UpdatePendingOvertimeRequest(
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    OvertimeApproverRoute ApproverRoute,
    string? Reason);
