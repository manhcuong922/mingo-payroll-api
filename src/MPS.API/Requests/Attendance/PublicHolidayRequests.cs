namespace MPS.API.Requests.Attendance;

public sealed record CreatePublicHolidayRequest(
    string Name,
    DateOnly HolidayDate,
    DateOnly? HolidayEndDate,
    string? Description,
    string ApplyScope = "AllEmployees",
    string? TargetDepartmentIds = null,
    string? TargetEmployeeIds = null);

public sealed record UpdatePublicHolidayRequest(
    string Name,
    DateOnly HolidayDate,
    DateOnly? HolidayEndDate,
    string? Description,
    string ApplyScope = "AllEmployees",
    string? TargetDepartmentIds = null,
    string? TargetEmployeeIds = null);
