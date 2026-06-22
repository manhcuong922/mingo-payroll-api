using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record CreatePublicHolidayRequest(
    string Name,
    DateOnly HolidayDate,
    PublicHolidayCategory Category,
    decimal? PayRateOverride,
    string? Description);

public sealed record UpdatePublicHolidayRequest(
    string Name,
    DateOnly HolidayDate,
    PublicHolidayCategory Category,
    decimal? PayRateOverride,
    string? Description);
