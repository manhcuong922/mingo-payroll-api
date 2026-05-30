using MediatR;

namespace MPS.Application.Features.Role.Commands;

public sealed record RoleCreateCommand(
    string Name,
    string? Description,
    IReadOnlyList<int> PermissionIds) : IRequest<int>;
