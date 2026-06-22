namespace MPS.API.Requests.Attendance;

public sealed record InitLeaveBalanceRequest(
    int EmployeeInformationId,
    int Year,
    int LeaveTypeId);

public sealed record CreateLeaveRequest(
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason,
    string? SupportingDocumentUrl);

public sealed record ReviewLeaveRequest(string? ReviewNote);

public sealed record CreateLeaveTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    decimal? PaidRate,
    decimal? MaxDaysPerRequest,
    bool RequireDocument,
    bool DeductsBalance);

public sealed record UpdateLeaveTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    decimal? PaidRate,
    decimal? MaxDaysPerRequest,
    bool RequireDocument,
    bool DeductsBalance,
    bool IsActive);
