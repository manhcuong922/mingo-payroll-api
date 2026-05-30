using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserUpdateCommandHandler : IRequestHandler<UserUpdateCommand, bool>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;

    public UserUpdateCommandHandler(IUserRepository repository, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(UserUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.User
        {
            Id = request.Id,
            EmployeeCode = request.EmployeeCode,
            FullName = request.FullName,
            RoleId = request.RoleId,
            Username = request.Username,
            PasswordHash = string.IsNullOrEmpty(request.Password)
                ? string.Empty
                : _passwordHasher.Hash(request.Password),
            Email = request.Email,
            Dob = request.Dob,
            PhoneNo = request.PhoneNo,
            Status = request.Status
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
