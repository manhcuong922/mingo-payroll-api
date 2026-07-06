using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeSlotTemplate.Commands;

/// <summary>
/// Command interface chung cho Create/Update — validator dùng chung để check
/// override multiplier &gt;= sàn luật, và enforce null-out inactive side theo IsNightSlot.
/// </summary>
public interface IOvertimeSlotTemplateCommand
{
    TimeOnly StartTime { get; }
    TimeOnly EndTime { get; }
    bool IsNightSlot { get; }
    decimal? MultiplierNormalDay { get; }
    decimal? MultiplierWeeklyRestDay { get; }
    decimal? MultiplierHolidayDay { get; }
    decimal? MultiplierNormalNightNoPrevOt { get; }
    decimal? MultiplierNormalNightWithPrevOt { get; }
    decimal? MultiplierWeeklyRestNight { get; }
    decimal? MultiplierHolidayNight { get; }
    string? Description { get; }
}

public sealed record CreateOvertimeSlotTemplateCommand(
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsNightSlot,
    decimal? MultiplierNormalDay,
    decimal? MultiplierWeeklyRestDay,
    decimal? MultiplierHolidayDay,
    decimal? MultiplierNormalNightNoPrevOt,
    decimal? MultiplierNormalNightWithPrevOt,
    decimal? MultiplierWeeklyRestNight,
    decimal? MultiplierHolidayNight,
    string? Description) : IRequest<int>, IOvertimeSlotTemplateCommand;

public sealed record UpdateOvertimeSlotTemplateCommand(
    int Id,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsNightSlot,
    decimal? MultiplierNormalDay,
    decimal? MultiplierWeeklyRestDay,
    decimal? MultiplierHolidayDay,
    decimal? MultiplierNormalNightNoPrevOt,
    decimal? MultiplierNormalNightWithPrevOt,
    decimal? MultiplierWeeklyRestNight,
    decimal? MultiplierHolidayNight,
    string? Description,
    bool IsActive) : IRequest<bool>, IOvertimeSlotTemplateCommand;

public sealed record DeleteOvertimeSlotTemplateCommand(int Id) : IRequest<bool>;
