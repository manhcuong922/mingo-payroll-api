using MediatR;
using MPS.Domain.Interfaces;


namespace MPS.Application.Features.User.Commands;

public class UserCreateCommandHandler : IRequestHandler<UserCreateCommand, string>
{
    private readonly IUserRepository _repository;

    public UserCreateCommandHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> Handle(UserCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.User
        {
            EmployeeCode = request.EmployeeCode,
            FullName = request.FullName,
            RoleId = request.RoleId,
            Username = request.Username,
            Password = request.Password,
            Email = request.Email,
            Dob = request.Dob,
            PhoneNo = request.PhoneNo,
            Status = request.Status
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
