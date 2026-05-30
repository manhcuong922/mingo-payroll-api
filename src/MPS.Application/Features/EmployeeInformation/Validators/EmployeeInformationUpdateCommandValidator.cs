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

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("UserId is required.")
            .MustAsync(async (userId, ct) => await repository.UserExistsAsync(userId, ct))
            .WithMessage("User does not exist.");

        RuleFor(x => x.PositionId)
            .GreaterThan(0).WithMessage("PositionId is required.")
            .MustAsync(async (positionId, ct) => await repository.PositionExistsAsync(positionId, ct))
            .WithMessage("Position does not exist.");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                var existing = await repository.GetByUserIdAsync(cmd.UserId, ct);
                return existing is null || existing.Id == cmd.Id;
            })
            .WithMessage("UserId is already used by another EmployeeInformation.");
    }
}
