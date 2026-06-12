using FluentValidation;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Validators;

public class EmployeeInformationCreateCommandValidator : AbstractValidator<EmployeeInformationCreateCommand>
{
    public EmployeeInformationCreateCommandValidator(IEmployeeInformationRepository repository)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        RuleFor(x => x.CitizenId)
            .NotEmpty().WithMessage("CitizenId is required.")
            .MaximumLength(20).WithMessage("CitizenId must not exceed 20 characters.");

        RuleFor(x => x.PermanentAddress)
            .NotEmpty().WithMessage("PermanentAddress is required.");

        RuleFor(x => x.UserId)
            .Must(userId => userId == null || userId > 0)
            .WithMessage("UserId must be greater than 0 when specified.")
            .MustAsync(async (userId, ct) => !userId.HasValue || await repository.UserExistsAsync(userId.Value, ct))
            .WithMessage("User does not exist.")
            .MustAsync(async (userId, ct) =>
                !userId.HasValue || await repository.GetByUserIdAsync(userId.Value, ct) is null)
            .WithMessage("EmployeeInformation already exists for this User.");

        RuleFor(x => x.PositionId)
            .GreaterThan(0).WithMessage("PositionId is required.")
            .MustAsync(async (positionId, ct) => await repository.PositionExistsAsync(positionId, ct))
            .WithMessage("Position does not exist.");

        RuleFor(x => x.DependentsNo)
            .GreaterThanOrEqualTo(0).WithMessage("DependentsNo must be 0 or greater.");
    }
}
