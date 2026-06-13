using FluentValidation;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Validators;

public class EmployeeInformationUpdateCommandValidator : AbstractValidator<EmployeeInformationUpdateCommand>
{
    public EmployeeInformationUpdateCommandValidator(IEmployeeInformationRepository repository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        RuleFor(x => x.CitizenId)
            .NotEmpty().WithMessage("CitizenId is required.")
            .MaximumLength(20).WithMessage("CitizenId must not exceed 20 characters.");

        RuleFor(x => x.PermanentAddress)
            .NotEmpty().WithMessage("PermanentAddress is required.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .MustAsync(async (cmd, email, ct) =>
                string.IsNullOrWhiteSpace(email) || !await repository.EmailExistsAsync(email!.Trim(), cmd.Id, ct))
            .WithMessage("Email is already used by another employee.");

        When(x => !string.IsNullOrWhiteSpace(x.GuardianFullName) || !string.IsNullOrWhiteSpace(x.GuardianPhoneNo), () =>
        {
            RuleFor(x => x.GuardianFullName)
                .NotEmpty().WithMessage("GuardianFullName is required when guardian info is provided.")
                .MaximumLength(200);

            RuleFor(x => x.GuardianPhoneNo)
                .NotEmpty().WithMessage("GuardianPhoneNo is required when guardian info is provided.")
                .MaximumLength(20);
        });

        RuleFor(x => x.UserId)
            .Must(userId => userId == null || userId > 0)
            .WithMessage("UserId must be greater than 0 when specified.")
            .MustAsync(async (userId, ct) => !userId.HasValue || await repository.UserExistsAsync(userId.Value, ct))
            .WithMessage("User does not exist.");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                if (!cmd.UserId.HasValue) return true;
                var existing = await repository.GetByUserIdAsync(cmd.UserId.Value, ct);
                return existing is null || existing.Id == cmd.Id;
            })
            .WithMessage("UserId is already used by another EmployeeInformation.");

        RuleFor(x => x.PositionId)
            .GreaterThan(0).WithMessage("PositionId is required.")
            .MustAsync(async (positionId, ct) => await repository.PositionExistsAsync(positionId, ct))
            .WithMessage("Position does not exist.");

        RuleFor(x => x.DependentsNo)
            .GreaterThanOrEqualTo(0).WithMessage("DependentsNo must be 0 or greater.");
    }
}
