using FluentValidation;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Validators;

public class SalaryGradeCreateCommandValidator : AbstractValidator<SalaryGradeCreateCommand>
{
    public SalaryGradeCreateCommandValidator(ISalaryGradeRepository repository)
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(50).WithMessage("Code must not exceed 50 characters.")
            .Matches("^[A-Za-z0-9_\\-]+$").WithMessage("Code may only contain letters, digits, hyphen and underscore.")
            .MustAsync(async (code, ct) => !await repository.CodeExistsAsync(code.Trim(), null, ct))
            .WithMessage("Code already exists.");

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

        RuleFor(x => x.Level)
            .GreaterThan(0).When(x => x.Level.HasValue)
            .WithMessage("Level must be greater than 0.");
    }
}
