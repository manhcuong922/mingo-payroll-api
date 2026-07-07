using MediatR;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public sealed record UpdatePublicHolidayCommand(
    int Id,
    string Name,
    DateOnly HolidayDate,
    DateOnly HolidayEndDate,
    string? Description,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds) : IRequest<bool>, IPublicHolidayTargetCommand;
