namespace MPS.Application.DTOs;

public sealed record RoleDetailDto(
    int Id,
    string Name,
    string? Description,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<PermissionDto> Permissions);
