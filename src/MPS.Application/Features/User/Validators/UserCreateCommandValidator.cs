using FluentValidation;
using MPS.Application.Features.User.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Validators;

public class UserCreateCommandValidator : AbstractValidator<UserCreateCommand>
{
    public UserCreateCommandValidator(IUserRepository repository)
    {
        RuleFor(x => x.EmployeeCode)
            .NotEmpty().WithMessage("EmployeeCode is required.")
            .MaximumLength(50).WithMessage("EmployeeCode must not exceed 50 characters.")
            .MustAsync(async (employeeCode, ct) => !await repository.EmployeeCodeExistsAsync(employeeCode, null, ct))
            .WithMessage("EmployeeCode already exists.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("RoleId is required.")
            .MustAsync(async (roleId, ct) => await repository.RoleExistsAsync(roleId, ct))
            .WithMessage("Role does not exist.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(100).WithMessage("Username must not exceed 100 characters.")
            .MustAsync(async (username, ct) => !await repository.UsernameExistsAsync(username, null, ct))
            .WithMessage("Username already exists.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is invalid.")
            .MustAsync(async (email, ct) => !await repository.EmailExistsAsync(email, null, ct))
            .WithMessage("Email already exists.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(255).WithMessage("Password must not exceed 255 characters.");
    }
}
