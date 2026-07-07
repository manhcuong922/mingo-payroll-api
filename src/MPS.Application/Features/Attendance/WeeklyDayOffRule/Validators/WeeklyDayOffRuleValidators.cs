using FluentValidation;
using MPS.Application.Features.Attendance.WeeklyDayOffRule.Commands;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Validators;

public class CreateWeeklyDayOffRuleCommandValidator : AbstractValidator<CreateWeeklyDayOffRuleCommand>
{
    public CreateWeeklyDayOffRuleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        Include(new WeeklyDayOffRuleTargetValidator<CreateWeeklyDayOffRuleCommand>());
    }
}

public class UpdateWeeklyDayOffRuleCommandValidator : AbstractValidator<UpdateWeeklyDayOffRuleCommand>
{
    public UpdateWeeklyDayOffRuleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        Include(new WeeklyDayOffRuleTargetValidator<UpdateWeeklyDayOffRuleCommand>());
    }
}

internal class WeeklyDayOffRuleTargetValidator<T> : AbstractValidator<T>
    where T : IWeeklyDayOffRuleWriteCommand
{
    private static readonly string[] AllowedScopes =
    {
        "AllEmployees",
        "AllDepartments",
        "SpecificDepartments",
        "SpecificEmployees"
    };

    public WeeklyDayOffRuleTargetValidator()
    {
        RuleFor(x => x.DaysOfWeek)
            .NotNull()
            .Must(days => days is { Count: > 0 })
            .WithMessage("DaysOfWeek must contain at least one day.")
            .Must(days => days.All(d => d is >= 0 and <= 6))
            .WithMessage("DaysOfWeek must be integers between 0 (Sunday) and 6 (Saturday).");

        RuleFor(x => x.ApplyScope)
            .NotEmpty()
            .MaximumLength(50)
            .Must(scope => AllowedScopes.Contains(scope))
            .WithMessage("ApplyScope is invalid.");

        RuleFor(x => x.TargetDepartmentIds).MaximumLength(1000);
        RuleFor(x => x.TargetEmployeeIds).MaximumLength(1000);

        When(x => x.ApplyScope == "SpecificDepartments", () =>
        {
            RuleFor(x => x.TargetDepartmentIds)
                .Must(ContainsNumericIds)
                .WithMessage("TargetDepartmentIds must contain one or more numeric department ids.");
        });

        When(x => x.ApplyScope == "SpecificEmployees", () =>
        {
            RuleFor(x => x.TargetEmployeeIds)
                .Must(ContainsNumericIds)
                .WithMessage("TargetEmployeeIds must contain one or more numeric employee ids.");
        });

        When(x => x.EffectiveTo.HasValue, () =>
        {
            RuleFor(x => x.EffectiveTo!.Value)
                .GreaterThanOrEqualTo(x => x.EffectiveFrom)
                .WithMessage("EffectiveTo must be on/after EffectiveFrom.");
        });
    }

    private static bool ContainsNumericIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return parts.Length > 0
            && parts.All(part => int.TryParse(part, out var id) && id > 0);
    }
}
