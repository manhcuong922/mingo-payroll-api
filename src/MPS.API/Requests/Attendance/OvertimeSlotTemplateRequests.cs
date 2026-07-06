namespace MPS.API.Requests.Attendance;

public sealed record CreateOvertimeSlotTemplateRequest(
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
    string? Description);

public sealed record UpdateOvertimeSlotTemplateRequest(
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
    bool IsActive);
