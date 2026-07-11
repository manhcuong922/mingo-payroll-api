using MPS.Application.DTOs.Attendance;
using LeaveRequestEntity = MPS.Domain.Entities.Attendance.LeaveRequest;

namespace MPS.Application.Features.Attendance.LeaveRequest.Common;

internal static class LeaveRequestMapper
{
    public static LeaveRequestDto ToDto(LeaveRequestEntity r) => new(
        r.Id,
        r.EmployeeInformationId,
        r.EmployeeInformation?.FullName ?? string.Empty,
        r.LeaveTypeId,
        r.LeaveType?.Code ?? string.Empty,
        r.LeaveType?.Name ?? string.Empty,
        r.StartDate,
        r.EndDate,
        r.TotalDays,
        r.IsPaid,
        r.Status,
        r.Reason,
        r.SupportingDocumentUrl,
        r.IsRetroactive,
        r.MedicalCertificateUrl,
        r.InsuranceCertNumber,
        r.ReviewNote,
        r.ReviewedByUserId,
        r.ReviewedAt,
        r.CancelledAt,
        r.CancelledByUserId,
        r.CancelReason,
        r.CreatedAt);
}
