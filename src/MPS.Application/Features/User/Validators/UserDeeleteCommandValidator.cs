using FluentValidation;
using MPS.Application.Features.User.Commands;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.User.Validators;
public class UserDeleteCommandValidator : AbstractValidator<UserDeleteCommand>
{
    public UserDeleteCommandValidator(IEmployeeInformationRepository employeeInformationRepository)
    {
        RuleFor(x => x.EmployeeCode)
            .NotEmpty().WithMessage("EmployeeCode is required.")
            .MustAsync(async (employeeCode, ct) =>
                await employeeInformationRepository.GetByEmployeeCodeAsync(employeeCode, ct) is null)
            .WithMessage("Cannot delete User because EmployeeInformation exists.");
    }
}
