namespace MPS.API.Requests.Department;

public sealed record CreateDepartmentRequest(
    string Code,
    string Name,
    string? Description,
    int? ManagerId);
