using FluentValidation;
using MPS.Application.Features.EmployeeInformation.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.EmployeeInformation.Validators;
public class EmployeeInformationDeleteCommandValidator : AbstractValidator<EmployeeInformationDeleteCommand>
{
    public EmployeeInformationDeleteCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");
    }
}
