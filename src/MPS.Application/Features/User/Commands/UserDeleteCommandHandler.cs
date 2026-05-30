using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserDeleteCommandHandler : IRequestHandler<UserDeleteCommand, bool>
{
    private readonly IUserRepository _repository;

    public UserDeleteCommandHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UserDeleteCommand request, CancellationToken cancellationToken) =>
        await _repository.DeleteAsync(request.Id, cancellationToken);
}
