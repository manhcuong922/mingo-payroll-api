using MediatR;

namespace MPS.Application.Features.Role.Commands;

public sealed record RoleUpdateCommand(
    int Id,
    string Name,
    string? Description) : IRequest<bool>;
