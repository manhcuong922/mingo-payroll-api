using FluentValidation;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.EmployeeInformation.Validators;
public class EmployeeInformationUpdateCommandValidator : AbstractValidator<EmployeeInformationUpdateCommand>
{
    public EmployeeInformationUpdateCommandValidator(IEmployeeInformationRepository repository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");

        RuleFor(x => x.EmployeeCode)
            .NotEmpty().WithMessage("EmployeeCode is required.")
            .MaximumLength(50).WithMessage("EmployeeCode must not exceed 50 characters.")
            .MustAsync(async (employeeCode, ct) => await repository.UserExistsAsync(employeeCode, ct))
            .WithMessage("User with EmployeeCode does not exist.");

        RuleFor(x => x.PositionId)
            .GreaterThan(0).WithMessage("PositionId is required.")
            .MustAsync(async (positionId, ct) => await repository.PositionExistsAsync(positionId, ct))
            .WithMessage("Position does not exist.");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                var existing = await repository.GetByEmployeeCodeAsync(cmd.EmployeeCode, ct);
                return existing is null || existing.Id == cmd.Id;
            })
            .WithMessage("EmployeeCode is already used by another EmployeeInformation.");
    }
}
