using MediatR;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public interface IPublicHolidayTargetCommand
{
    string ApplyScope { get; }
    string? TargetDepartmentIds { get; }
    string? TargetEmployeeIds { get; }
}

public sealed record CreatePublicHolidayCommand(
    string Name,
    DateOnly HolidayDate,
    DateOnly HolidayEndDate,
    string? Description,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds) : IRequest<int>, IPublicHolidayTargetCommand;
