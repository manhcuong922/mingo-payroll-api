using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record CreateLeaveRequestCommand(
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason,
    string? SupportingDocumentUrl,
    string? MedicalCertificateUrl,
    string? InsuranceCertNumber,
    OvertimeApproverRoute? ApproverRoute,
    string? OverrideReason) : IRequest<CreateLeaveRequestResult>;

/// <summary>
/// Kết quả tạo đơn kèm cờ báo hệ thống đã tự chuyển sang loại UNPAID (khi hết quỹ phép + LeaveType.AutoFallbackToUnpaid=true).
/// </summary>
public sealed record CreateLeaveRequestResult(
    int Id,
    int FinalLeaveTypeId,
    string FinalLeaveTypeCode,
    bool SwitchedToUnpaid,
    string? Message);
