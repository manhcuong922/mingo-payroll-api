namespace MPS.API.Requests.Role;

public sealed record CreateRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<int> PermissionIds);
