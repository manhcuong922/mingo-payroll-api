namespace MPS.API.Requests.Attendance;

public sealed record CreatePublicHolidayRequest(
    string Name,
    DateOnly HolidayDate,
    DateOnly? HolidayEndDate,
    string? Description);

public sealed record UpdatePublicHolidayRequest(
    string Name,
    DateOnly HolidayDate,
    DateOnly? HolidayEndDate,
    string? Description);
