namespace MPS.Application.DTOs;

/// <summary>
/// DTO - dữ liệu truyền cho PerformanceMetric giữa các layer.
/// </summary>
public record PerformanceMetricDto(
    int Id,
    string Name,
    string Description,
    decimal Weight,
    IReadOnlyList<RoleDto> TargetRoles);

/// <summary>
/// DTO gọn cho Role dùng kèm trong PerformanceMetric.
/// </summary>
public record RoleDto(int Id, string Name);
