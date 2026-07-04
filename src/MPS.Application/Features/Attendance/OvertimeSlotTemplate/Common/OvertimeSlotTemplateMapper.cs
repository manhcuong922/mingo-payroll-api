using MPS.Application.DTOs.Attendance;
using OvertimeSlotTemplateEntity = MPS.Domain.Entities.Attendance.OvertimeSlotTemplate;

namespace MPS.Application.Features.Attendance.OvertimeSlotTemplate.Common;

internal static class OvertimeSlotTemplateMapper
{
    public static OvertimeSlotTemplateDto ToDto(OvertimeSlotTemplateEntity e) =>
        new(
            e.Id,
            e.Code,
            e.StartTime,
            e.EndTime,
            e.IsNightSlot,
            e.MultiplierNormalDay,
            e.MultiplierWeeklyRestDay,
            e.MultiplierHolidayDay,
            e.MultiplierNormalNightNoPrevOt,
            e.MultiplierNormalNightWithPrevOt,
            e.MultiplierWeeklyRestNight,
            e.MultiplierHolidayNight,
            e.Description,
            e.IsActive,
            e.CreatedAt,
            e.UpdatedAt);

    public static OvertimeSlotSummaryDto ToSummary(OvertimeSlotTemplateEntity e) =>
        new(e.Id, e.Code, e.StartTime, e.EndTime, e.IsActive);
}
