using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserCreateCommandHandler : IRequestHandler<UserCreateCommand, int>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;

    public UserCreateCommandHandler(IUserRepository repository, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    public async Task<int> Handle(UserCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.User
        {
            EmployeeCode = request.EmployeeCode,
            FullName = request.FullName,
            RoleId = request.RoleId,
            Username = request.Username,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Email = request.Email,
            Dob = request.Dob,
            PhoneNo = request.PhoneNo,
            Status = request.Status,
            PasswordChangedAt = DateTime.UtcNow,
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
