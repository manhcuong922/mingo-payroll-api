using FluentValidation;
using MPS.Application.Features.Department.Commands;

namespace MPS.Application.Features.Department.Validators;

public class DepartmentDeleteCommandValidator : AbstractValidator<DepartmentDeleteCommand>
{
    public DepartmentDeleteCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id is required.");
    }
}
