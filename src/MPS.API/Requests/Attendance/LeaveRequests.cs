using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record InitLeaveBalanceRequest(
    int EmployeeInformationId,
    int Year,
    int LeaveTypeId);

public sealed class CreateLeaveRequest
{
    [FromForm(Name = "leaveTypeId")]
    public int LeaveTypeId { get; set; }

    [FromForm(Name = "startDate")]
    public DateOnly StartDate { get; set; }

    [FromForm(Name = "endDate")]
    public DateOnly EndDate { get; set; }

    [FromForm(Name = "reason")]
    public string? Reason { get; set; }

    [FromForm(Name = "insuranceCertNumber")]
    public string? InsuranceCertNumber { get; set; }

    [FromForm(Name = "approverRoute")]
    public OvertimeApproverRoute? ApproverRoute { get; set; }

    [FromForm(Name = "overrideReason")]
    public string? OverrideReason { get; set; }

    [FromForm(Name = "medicalCertificate")]
    public IFormFile? MedicalCertificate { get; set; }
}

public sealed record ReviewLeaveRequest(string? ReviewNote);

public sealed class ApproveLeaveRequestBody
{
    [FromForm(Name = "reviewNote")]
    public string? ReviewNote { get; set; }

    [FromForm(Name = "insuranceCertNumber")]
    public string? InsuranceCertNumber { get; set; }

    [FromForm(Name = "overrideReason")]
    public string? OverrideReason { get; set; }

    [FromForm(Name = "medicalCertificate")]
    public IFormFile? MedicalCertificate { get; set; }
}

public sealed class UploadLeaveMedicalCertificateRequest
{
    [FromForm(Name = "medicalCertificate")]
    public IFormFile MedicalCertificate { get; set; } = default!;

    [FromForm(Name = "overrideReason")]
    public string? OverrideReason { get; set; }
}

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
