using MPS.Application.DTOs.Attendance;
using WfhRequestEntity = MPS.Domain.Entities.Attendance.WfhRequest;

namespace MPS.Application.Features.Attendance.WfhRequest.Common;

internal static class WfhRequestMapper
{
    public static WfhRequestDto ToDto(WfhRequestEntity r) => new(
        r.Id,
        r.EmployeeInformationId,
        r.EmployeeInformation?.FullName ?? string.Empty,
        r.StartDate,
        r.EndDate,
        r.TotalDays,
        r.WorkPlan,
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
