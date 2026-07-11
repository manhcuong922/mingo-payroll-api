using MPS.Application.DTOs.Attendance;
using LateEarlyRequestEntity = MPS.Domain.Entities.Attendance.LateEarlyRequest;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Common;

internal static class LateEarlyRequestMapper
{
    public static LateEarlyRequestDto ToDto(LateEarlyRequestEntity r) => new(
        r.Id,
        r.EmployeeInformationId,
        r.EmployeeInformation?.FullName ?? string.Empty,
        r.WorkDate,
        r.Kind,
        r.Minutes,
        r.IsUnplanned,
        r.DeductFromLeaveBalance,
        r.ApproverRoute,
        r.Status,
        r.Reason,
        r.ReviewNote,
        r.ReviewedByUserId,
        r.ReviewedAt,
        r.CancelledAt,
        r.CancelledByUserId,
        r.CancelReason,
        r.CreatedAt);
}
