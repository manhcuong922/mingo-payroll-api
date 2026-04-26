namespace MPS.Application.DTOs;

public record PerformanceMetricDto(
    int Id,
    string Name,
    string Description,
    decimal Weight,
    int TypePerformanceMetricId,
    IReadOnlyList<RoleDto> TargetRoles);

public record RoleDto(int Id, string Name);
