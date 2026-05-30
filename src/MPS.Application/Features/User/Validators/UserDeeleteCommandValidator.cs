using FluentValidation;
using MPS.Application.Features.User.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Validators;

public class UserDeleteCommandValidator : AbstractValidator<UserDeleteCommand>
{
    public UserDeleteCommandValidator(IEmployeeInformationRepository employeeInformationRepository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.")
            .MustAsync(async (userId, ct) =>
                await employeeInformationRepository.GetByUserIdAsync(userId, ct) is null)
            .WithMessage("Cannot delete User because EmployeeInformation exists.");
    }
}
