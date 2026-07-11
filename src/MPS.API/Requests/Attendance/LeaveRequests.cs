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
    string? SupportingDocumentUrl,
    string? MedicalCertificateUrl,
    string? InsuranceCertNumber,
    string? OverrideReason);

public sealed record ReviewLeaveRequest(string? ReviewNote);

public sealed record ApproveLeaveRequestBody(
    string? ReviewNote,
    string? MedicalCertificateUrl,
    string? InsuranceCertNumber,
    string? OverrideReason);

public sealed record CancelLeaveRequestBody(string? CancelReason);

public sealed record CreateLeaveTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    decimal? PaidRate,
    decimal? MaxDaysPerRequest,
    bool AllowEmployeeSelection,
    decimal? MaxDaysPerYear,
    decimal? MaxDaysPerMonth,
    decimal? MaxDaysPerWeek,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
    bool RequireDocument,
    bool DeductsBalance,
    int? MinNoticeDaysForShort,
    int? MinNoticeDaysForMedium,
    int? MinNoticeDaysForLong,
    bool AutoFallbackToUnpaid,
    bool AllowRetroactive,
    bool IsActive = true);

public sealed record UpdateLeaveTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    decimal? PaidRate,
    decimal? MaxDaysPerRequest,
    bool AllowEmployeeSelection,
    decimal? MaxDaysPerYear,
    decimal? MaxDaysPerMonth,
    decimal? MaxDaysPerWeek,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
    bool RequireDocument,
    bool DeductsBalance,
    int? MinNoticeDaysForShort,
    int? MinNoticeDaysForMedium,
    int? MinNoticeDaysForLong,
    bool AutoFallbackToUnpaid,
    bool AllowRetroactive,
    bool IsActive);
