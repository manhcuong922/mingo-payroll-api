using FluentValidation;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Validators;

public class EmployeeInformationCreateCommandValidator : AbstractValidator<EmployeeInformationCreateCommand>
{
    public EmployeeInformationCreateCommandValidator(IEmployeeInformationRepository repository)
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("UserId is required.")
            .MustAsync(async (userId, ct) => await repository.UserExistsAsync(userId, ct))
            .WithMessage("User does not exist.")
            .MustAsync(async (userId, ct) =>
                await repository.GetByUserIdAsync(userId, ct) is null)
            .WithMessage("EmployeeInformation already exists for this User.");

        RuleFor(x => x.PositionId)
            .GreaterThan(0).WithMessage("PositionId is required.")
            .MustAsync(async (positionId, ct) => await repository.PositionExistsAsync(positionId, ct))
            .WithMessage("Position does not exist.");
    }
}
