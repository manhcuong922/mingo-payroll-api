namespace MPS.API.Requests.Role;

public sealed record UpdateRolePermissionsRequest(IReadOnlyList<int> PermissionIds);
