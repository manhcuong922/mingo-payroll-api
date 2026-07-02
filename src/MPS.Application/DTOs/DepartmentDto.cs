namespace MPS.Application.DTOs;

public record DepartmentDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int? ManagerId,
    string? ManagerName,
    bool IsActive,
    int EmployeeCount);
