namespace MPS.API.Requests.Department;

public sealed record UpdateDepartmentRequest(
    string Code,
    string Name,
    string? Description,
    int? ManagerId);
