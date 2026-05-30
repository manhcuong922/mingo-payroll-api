namespace MPS.Application.DTOs;

public sealed record RoleDto(
    int Id,
    string Name,
    string? Description,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int PermissionCount);
