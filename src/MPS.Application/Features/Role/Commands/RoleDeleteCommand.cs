using MediatR;

namespace MPS.Application.Features.Role.Commands;

public sealed record RoleDeleteCommand(int Id) : IRequest<bool>;
