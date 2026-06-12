using FluentValidation;
using MPS.Application.Features.SalaryGrade.Commands;

namespace MPS.Application.Features.SalaryGrade.Validators;

public class SalaryGradeUpdateCommandValidator : AbstractValidator<SalaryGradeUpdateCommand>
{
    public SalaryGradeUpdateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.BaseSalary)
            .GreaterThan(0).WithMessage("BaseSalary must be greater than 0.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .Matches("^[A-Za-z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}
