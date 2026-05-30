using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserUpdateCommandHandler : IRequestHandler<UserUpdateCommand, bool>
{
    private readonly IUserRepository _repository;

    public UserUpdateCommandHandler(IUserRepository repository)
    {
        _repository = repository;
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
            PasswordHash = request.Password ?? string.Empty,
            Email = request.Email,
            Dob = request.Dob,
            PhoneNo = request.PhoneNo,
            Status = request.Status
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
