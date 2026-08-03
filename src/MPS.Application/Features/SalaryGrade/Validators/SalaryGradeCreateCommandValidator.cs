using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Validators;

public class SalaryGradeCreateCommandValidator : AbstractValidator<SalaryGradeCreateCommand>
{
    public SalaryGradeCreateCommandValidator(
        IPositionRepository positionRepository,
        IComplianceLookupService complianceLookup)
    {
        RuleFor(x => x.PositionId)
            .GreaterThan(0).WithMessage("PositionId is required.")
            .MustAsync(async (id, ct) => await positionRepository.GetByIdAsync(id, ct) is not null)
            .WithMessage("Position does not exist.");

        RuleFor(x => x.Level)
            .GreaterThan(0).WithMessage("Level must be greater than 0.");

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

        MinimumWageRule.AddMinimumWageRule(this, complianceLookup, x => x.BaseSalary, x => x.Currency);
    }
}
