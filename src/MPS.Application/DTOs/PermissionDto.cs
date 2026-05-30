namespace MPS.Application.DTOs;

public sealed record PermissionDto(
    int Id,
    string Code,
    string Resource,
    string Action,
    string Scope,
    string Description);
