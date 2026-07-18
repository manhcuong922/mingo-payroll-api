using MPS.Application.DTOs.Attendance;
using ScheduleTemplateEntity = MPS.Domain.Entities.Attendance.ScheduleTemplate;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Common;

internal static class ScheduleTemplateMapper
{
    public static ScheduleTemplateDto ToDto(ScheduleTemplateEntity entity)
    {
        var shifts = entity.Shifts
            .Where(s => s.ShiftTemplate is not null)
            .OrderBy(s => s.ShiftTemplate.StartTime)
            .ThenBy(s => s.ShiftTemplate.Code)
            .Select(s => new ScheduleTemplateShiftDto(
                s.ShiftTemplateId,
                s.ShiftTemplate.Code,
                s.ShiftTemplate.Name,
                s.ShiftTemplate.StartTime,
                s.ShiftTemplate.EndTime))
            .ToList();

        if (shifts.Count == 0 && entity.ShiftTemplate is not null)
        {
            shifts.Add(new ScheduleTemplateShiftDto(
                entity.ShiftTemplateId,
                entity.ShiftTemplate.Code,
                entity.ShiftTemplate.Name,
                entity.ShiftTemplate.StartTime,
                entity.ShiftTemplate.EndTime));
        }

        var primary = shifts.FirstOrDefault();

        return new ScheduleTemplateDto(
            entity.Id,
            entity.Name,
            primary?.ShiftTemplateId ?? entity.ShiftTemplateId,
            primary?.ShiftTemplateCode ?? entity.ShiftTemplate?.Code ?? string.Empty,
            primary?.ShiftTemplateName ?? entity.ShiftTemplate?.Name ?? string.Empty,
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
            shifts.Select(s => s.ShiftTemplateId).ToList(),
            shifts,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ApplyTarget,
            ScheduleTemplateValidation.FromCsvMask(entity.TargetDepartmentIds),
            entity.IncludeRestDaysAndHolidays);
    }
}
