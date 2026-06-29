using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public sealed record CreateScheduleTemplateCommand(
    string Name,
    int ShiftTemplateId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    ScheduleRepeatUnit RepeatUnit,
    int RepeatEvery,
    IReadOnlyList<int>? DaysOfWeek,
    IReadOnlyList<int>? DaysOfMonth,
    IReadOnlyList<int> EmployeeInformationIds,
    string? Notes,
    bool AcknowledgeWeeklyOvertimeWarnings = false) : IRequest<ScheduleTemplateApplyResultDto>;
