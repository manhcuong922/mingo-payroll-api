namespace MPS.API.Requests.Role;

public sealed record UpdateRoleRequest(
    string Name,
    string? Description);
