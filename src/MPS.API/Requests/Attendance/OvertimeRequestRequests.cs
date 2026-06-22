namespace MPS.API.Requests.Attendance;

public sealed record CreateOvertimeRequest(
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    string? Reason);

public sealed record ReviewOvertimeRequest(string? ReviewNote);
