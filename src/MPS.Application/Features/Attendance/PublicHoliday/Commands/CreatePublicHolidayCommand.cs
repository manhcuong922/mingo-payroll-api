using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public sealed record CreatePublicHolidayCommand(
    string Name,
    DateOnly HolidayDate,
    PublicHolidayCategory Category,
    decimal? PayRateOverride,
    string? Description) : IRequest<int>;
