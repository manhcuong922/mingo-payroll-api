using FluentValidation;
using MPS.Application.Features.User.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Validators;

public class UserCreateCommandValidator : AbstractValidator<UserCreateCommand>
{
    public UserCreateCommandValidator(IUserRepository repository, IEmployeeInformationRepository eiRepository)
    {
        RuleFor(x => x.EmployeeInformationId)
            .GreaterThan(0).WithMessage("EmployeeInformationId is required.")
            .MustAsync(async (id, ct) => await eiRepository.GetByIdAsync(id, ct) is not null)
            .WithMessage("EmployeeInformation does not exist.");

        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("RoleId is required.")
            .MustAsync(async (roleId, ct) => await repository.RoleExistsAsync(roleId, ct))
            .WithMessage("Role does not exist.");
    }
}
