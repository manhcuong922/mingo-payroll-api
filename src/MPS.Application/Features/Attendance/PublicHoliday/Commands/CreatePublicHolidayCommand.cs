using MediatR;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public sealed record CreatePublicHolidayCommand(
    string Name,
    DateOnly HolidayDate,
    DateOnly HolidayEndDate,
    string? Description) : IRequest<int>;
