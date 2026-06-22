using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public sealed record UpdatePublicHolidayCommand(
    int Id,
    string Name,
    DateOnly HolidayDate,
    PublicHolidayCategory Category,
    decimal? PayRateOverride,
    string? Description) : IRequest<bool>;
