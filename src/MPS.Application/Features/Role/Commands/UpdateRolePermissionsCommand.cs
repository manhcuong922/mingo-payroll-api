using MediatR;

namespace MPS.Application.Features.Role.Commands;

public sealed record UpdateRolePermissionsCommand(
    int RoleId,
    IReadOnlyList<int> PermissionIds) : IRequest<bool>;
