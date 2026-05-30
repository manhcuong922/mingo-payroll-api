using MediatR;

namespace MPS.Application.Features.User.Commands;

public record UserDeleteCommand(int Id) : IRequest<bool>;
