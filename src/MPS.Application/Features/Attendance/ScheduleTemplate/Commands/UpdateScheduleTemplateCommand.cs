using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public sealed record UpdateScheduleTemplateCommand(
    int Id,
    string Name,
    IReadOnlyList<int> ShiftTemplateIds,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    ScheduleRepeatUnit RepeatUnit,
    int RepeatEvery,
    IReadOnlyList<int>? DaysOfWeek,
    IReadOnlyList<int>? DaysOfMonth,
    IReadOnlyList<int> EmployeeInformationIds,
    bool IsActive,
    string? Notes) : IRequest<Unit>;
