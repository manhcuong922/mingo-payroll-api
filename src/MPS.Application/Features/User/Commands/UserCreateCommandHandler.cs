using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserCreateCommandHandler : IRequestHandler<UserCreateCommand, int>
{
    private readonly IUserRepository _repository;

    public UserCreateCommandHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(UserCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.User
        {
            EmployeeCode = request.EmployeeCode,
            FullName = request.FullName,
            RoleId = request.RoleId,
            Username = request.Username,
            PasswordHash = request.Password,
            Email = request.Email,
            Dob = request.Dob,
            PhoneNo = request.PhoneNo,
            Status = request.Status,
            PasswordChangedAt = DateTime.UtcNow,
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
