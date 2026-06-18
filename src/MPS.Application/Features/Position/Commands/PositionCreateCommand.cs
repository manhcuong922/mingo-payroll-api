using MediatR;

namespace MPS.Application.Features.Position.Commands;

public sealed record PositionCreateCommand(string Code, string Name, string? Description) : IRequest<int>;
