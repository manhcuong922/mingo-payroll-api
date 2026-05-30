namespace MPS.Application.DTOs;

public record PerformanceMetricDto(
    int Id,
    string Name,
    string Description,
    decimal Weight,
    int TypePerformanceMetricId,
    IReadOnlyList<RoleSummaryDto> TargetRoles);

public record RoleSummaryDto(int Id, string Name);
