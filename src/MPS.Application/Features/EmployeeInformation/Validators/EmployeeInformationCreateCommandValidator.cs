using FluentValidation;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Domain.Interfaces;


namespace MPS.Application.Features.EmployeeInformation.Validators;
public class EmployeeInformationCreateCommandValidator : AbstractValidator<EmployeeInformationCreateCommand>
{
    public EmployeeInformationCreateCommandValidator(IEmployeeInformationRepository repository)
    {
        RuleFor(x => x.EmployeeCode)
            .NotEmpty().WithMessage("EmployeeCode is required.")
            .MaximumLength(50).WithMessage("EmployeeCode must not exceed 50 characters.")
            .MustAsync(async (employeeCode, ct) => await repository.UserExistsAsync(employeeCode, ct))
            .WithMessage("User with EmployeeCode does not exist.")
            .MustAsync(async (employeeCode, ct) =>
                await repository.GetByEmployeeCodeAsync(employeeCode, ct) is null)
            .WithMessage("EmployeeInformation already exists for this EmployeeCode.");

        RuleFor(x => x.PositionId)
            .GreaterThan(0).WithMessage("PositionId is required.")
            .MustAsync(async (positionId, ct) => await repository.PositionExistsAsync(positionId, ct))
            .WithMessage("Position does not exist.");
    }
}
