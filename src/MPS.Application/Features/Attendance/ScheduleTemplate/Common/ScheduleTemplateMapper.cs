using MPS.Application.DTOs.Attendance;
using ScheduleTemplateEntity = MPS.Domain.Entities.Attendance.ScheduleTemplate;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Common;

internal static class ScheduleTemplateMapper
{
    public static ScheduleTemplateDto ToDto(ScheduleTemplateEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.ShiftTemplateId,
            entity.ShiftTemplate?.Code ?? string.Empty,
            entity.ShiftTemplate?.Name ?? string.Empty,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.RepeatUnit,
            entity.RepeatEvery,
            ScheduleTemplateValidation.FromCsvMask(entity.DaysOfWeekMask),
            ScheduleTemplateValidation.FromCsvMask(entity.DaysOfMonthMask),
            entity.IsActive,
            entity.Notes,
            entity.Employees.Select(e => new ScheduleTemplateEmployeeDto(
                e.EmployeeInformationId,
                e.EmployeeInformation?.FullName ?? string.Empty,
                e.EmployeeInformation?.User?.EmployeeCode)).ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);
}
