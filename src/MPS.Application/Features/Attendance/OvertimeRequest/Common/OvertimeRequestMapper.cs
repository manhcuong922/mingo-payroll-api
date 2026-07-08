using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Common;
using OvertimeRequestEntity = MPS.Domain.Entities.Attendance.OvertimeRequest;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

internal static class OvertimeRequestMapper
{
    public static OvertimeRequestDto ToDto(OvertimeRequestEntity r)
    {
        var slotSummary = r.OvertimeSlotTemplate is not null
            ? OvertimeSlotTemplateMapper.ToSummary(r.OvertimeSlotTemplate)
            : null;

        return new OvertimeRequestDto(
            r.Id,
            r.EmployeeInformationId,
            r.EmployeeInformation?.FullName ?? string.Empty,
            r.WorkDate,
            r.OtStartTime,
            r.OtEndTime,
            r.OtHours,
            r.OtDayHours,
            r.OtNightHours,
            r.OvertimeSlotTemplateId,
            slotSummary,
            r.Status,
            r.Reason,
            r.ReviewNote,
            r.ReviewedByUserId,
            r.ReviewedAt,
            r.CreatedAt,
            r.ProjectId,
            r.Project?.Title,
            r.ApproverRoute,
            r.BatchId);
    }
}
