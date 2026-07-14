using MPS.Application.DTOs.Attendance;
using OvertimeRequestEntity = MPS.Domain.Entities.Attendance.OvertimeRequest;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

internal static class OvertimeRequestMapper
{
    public static OvertimeRequestDto ToDto(OvertimeRequestEntity r) =>
        new(
            r.Id,
            r.EmployeeInformationId,
            r.EmployeeInformation?.FullName ?? string.Empty,
            r.WorkDate,
            r.OtStartTime,
            r.OtEndTime,
            r.OtHours,
            r.OtDayHours,
            r.OtNightHours,
            r.LaborLawConstantsVersionId,
            r.Status,
            r.Reason,
            r.ReviewNote,
            r.ReviewedByUserId,
            r.ReviewedAt,
            r.CreatedAt,
            r.ProjectId,
            r.Project?.Title,
            r.ApproverRoute,
            r.BatchId,
            r.PmReviewedByUserId,
            r.PmReviewedAt,
            r.PmReviewNote,
            r.ApproverReviewedByUserId,
            r.ApproverReviewedAt,
            r.ApproverReviewNote);
}
