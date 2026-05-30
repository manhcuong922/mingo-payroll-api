using MediatR;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.User.Commands;
public class UserDeleteCommandHandler : IRequestHandler<UserDeleteCommand, bool>
{
    private readonly IUserRepository _repository;

    public UserDeleteCommandHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UserDeleteCommand request, CancellationToken cancellationToken)
    {
        return await _repository.DeleteAsync(request.EmployeeCode, cancellationToken);
    }
}
