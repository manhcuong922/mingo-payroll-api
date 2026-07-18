namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public interface IShiftTemplateCommand
{
    string Code { get; }
    string Name { get; }
    TimeOnly StartTime { get; }
    TimeOnly EndTime { get; }
    TimeOnly? BreakStartTime { get; }
    TimeOnly? BreakEndTime { get; }
    decimal StandardHours { get; }
    decimal StandardDays { get; }
    string? Notes { get; }
}
