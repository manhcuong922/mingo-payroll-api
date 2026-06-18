using MediatR;

namespace MPS.Application.Features.Position.Commands;

public sealed record PositionUpdateCommand(int Id, string Code, string Name, string? Description) : IRequest<bool>;
