namespace MPS.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Username { get; }
    int? RoleId { get; }
    string? RoleName { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsAuthenticated { get; }

    bool HasPermission(string permissionCode);
}
