using FluentValidation;
using MPS.Application.Features.Contract.Commands;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.Contract.Validators;

/// <summary>
/// Stateless validation rules shared by Create and Update commands.
/// Pure: no repository / DB dependency (handler-level checks live in ContractPreflightChecker).
/// </summary>
internal static class ContractValidationRules
{
    public static void ApplyCommonRules<TCommand>(AbstractValidator<TCommand> v, Func<TCommand, ContractCore> selector)
        where TCommand : class
    {
        v.RuleFor(x => selector(x).ContractNo)
            .NotEmpty().WithMessage("ContractNo is required.")
            .MaximumLength(50).WithMessage("ContractNo must not exceed 50 characters.");

        v.RuleFor(x => selector(x).JobDescription)
            .NotEmpty().WithMessage("JobDescription is required.");

        v.RuleFor(x => selector(x).WorkLocation)
            .NotEmpty().WithMessage("WorkLocation is required.")
            .MaximumLength(500);

        v.RuleFor(x => selector(x).WorkingTimeJson)
            .NotEmpty().WithMessage("WorkingTimeJson is required.");

        v.RuleFor(x => selector(x).ContractText)
            .NotEmpty().WithMessage("ContractText is required.");

        v.RuleFor(x => selector(x).EmployeeId).GreaterThan(0);
        v.RuleFor(x => selector(x).EmployerPartyId).GreaterThan(0);
        v.RuleFor(x => selector(x).EmployerSignerUserId).GreaterThan(0);
        v.RuleFor(x => selector(x).PositionId).GreaterThan(0);
        v.RuleFor(x => selector(x).SalaryGradeId).GreaterThan(0);

        v.RuleFor(x => selector(x).EffectiveFrom).NotEmpty();

        // EffectiveFrom < EffectiveTo when EffectiveTo present
        v.RuleFor(x => selector(x))
            .Must(c => c.EffectiveTo is null || c.EffectiveFrom < c.EffectiveTo)
            .WithMessage("EffectiveFrom must be earlier than EffectiveTo.")
            .OverridePropertyName("EffectiveFrom");

        // Category=Definite ⇒ EffectiveTo not null AND months ≤ 36 (Điều 20 1b)
        v.RuleFor(x => selector(x))
            .Must(c => c.ContractCategory != ContractCategory.Definite || c.EffectiveTo is not null)
            .WithMessage("Definite contracts must have EffectiveTo. (Điều 20 BLLĐ 2019)")
            .OverridePropertyName("EffectiveTo");

        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (c.ContractCategory != ContractCategory.Definite || c.EffectiveTo is null) return true;
                var months = MonthsBetween(c.EffectiveFrom, c.EffectiveTo.Value);
                return months <= LaborLawConstants.ContractDuration.DefiniteTermMaxMonths;
            })
            .WithMessage($"Definite contract duration must not exceed {LaborLawConstants.ContractDuration.DefiniteTermMaxMonths} months. (Điều 20 1b BLLĐ 2019)")
            .OverridePropertyName("EffectiveTo");

        // Category=Indefinite ⇒ EffectiveTo null
        v.RuleFor(x => selector(x))
            .Must(c => c.ContractCategory != ContractCategory.Indefinite || c.EffectiveTo is null)
            .WithMessage("Indefinite contracts must NOT have EffectiveTo.")
            .OverridePropertyName("EffectiveTo");

        // Indefinite + Probation cấm
        v.RuleFor(x => selector(x))
            .Must(c => !(c.ContractCategory == ContractCategory.Indefinite && c.ContractSubtype == ContractSubtype.Probation))
            .WithMessage("Combination Category=Indefinite + Subtype=Probation is not allowed (Điều 25 BLLĐ 2019).")
            .OverridePropertyName("ContractSubtype");

        // Form=Verbal ⇒ duration < 30 days
        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (c.Form != ContractForm.Verbal) return true;
                if (c.EffectiveTo is null) return false;
                return (c.EffectiveTo.Value - c.EffectiveFrom).TotalDays < LaborLawConstants.ContractForm.MinDurationForWrittenFormDays;
            })
            .WithMessage($"Verbal form is only allowed for contracts shorter than {LaborLawConstants.ContractForm.MinDurationForWrittenFormDays} days (Điều 14 k.2 BLLĐ 2019).")
            .OverridePropertyName("Form");

        // HasProbation true ⇒ 4 trường bắt buộc
        v.RuleFor(x => selector(x))
            .Must(c => !c.HasProbation || c.ProbationStartDate.HasValue)
            .WithMessage("ProbationStartDate is required when HasProbation is true.")
            .OverridePropertyName("ProbationStartDate");

        v.RuleFor(x => selector(x))
            .Must(c => !c.HasProbation || c.ProbationEndDate.HasValue)
            .WithMessage("ProbationEndDate is required when HasProbation is true.")
            .OverridePropertyName("ProbationEndDate");

        v.RuleFor(x => selector(x))
            .Must(c => !c.HasProbation || c.ProbationSalary.HasValue)
            .WithMessage("ProbationSalary is required when HasProbation is true.")
            .OverridePropertyName("ProbationSalary");

        v.RuleFor(x => selector(x))
            .Must(c => !c.HasProbation || c.ProbationResultStatus.HasValue)
            .WithMessage("ProbationResultStatus is required when HasProbation is true.")
            .OverridePropertyName("ProbationResultStatus");

        // HasProbation ⇒ duration ≥ 30 ngày (Điều 24 k.3)
        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (!c.HasProbation) return true;
                if (c.EffectiveTo is null) return true; // Indefinite covered by combination rule above
                return (c.EffectiveTo.Value - c.EffectiveFrom).TotalDays >= LaborLawConstants.Probation.NotAllowedBelowDays;
            })
            .WithMessage($"Probation is not allowed for contracts shorter than {LaborLawConstants.Probation.NotAllowedBelowDays} days (Điều 24 k.3 BLLĐ 2019).")
            .OverridePropertyName("HasProbation");

        // ProbationEnd - ProbationStart ≤ 180 ngày (max — handler can tighten by Position level later)
        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (!c.HasProbation || !c.ProbationStartDate.HasValue || !c.ProbationEndDate.HasValue) return true;
                var days = (c.ProbationEndDate.Value - c.ProbationStartDate.Value).TotalDays;
                return days > 0 && days <= LaborLawConstants.Probation.MaxDaysEnterpriseManager;
            })
            .WithMessage($"Probation period must be > 0 and ≤ {LaborLawConstants.Probation.MaxDaysEnterpriseManager} days (Điều 25 BLLĐ 2019).")
            .OverridePropertyName("ProbationEndDate");

        // Allowances: no duplicates + Amount ≥ 0
        v.RuleFor(x => selector(x).Allowances)
            .Must(list => list.Select(a => a.AllowanceTypeId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate AllowanceTypeId in payload.");

        v.RuleForEach(x => selector(x).Allowances).ChildRules(child =>
        {
            child.RuleFor(a => a.AllowanceTypeId).GreaterThan(0);
            child.RuleFor(a => a.Amount).GreaterThanOrEqualTo(0);
            child.RuleFor(a => a.Currency)
                .Must(c => string.IsNullOrEmpty(c) || (c.Length == 3 && c.All(char.IsLetter)))
                .WithMessage("Allowance Currency must be a 3-letter ISO 4217 code if specified.");
            child.RuleFor(a => a.Note).MaximumLength(500);
        });
    }

    private static int MonthsBetween(DateTime from, DateTime to)
    {
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        if (to.Day < from.Day) months--;
        return Math.Max(0, months);
    }
}

/// <summary>Adapter exposing the subset of fields shared by Create/Update commands to the rules above.</summary>
internal sealed record ContractCore(
    string ContractNo,
    ContractCategory ContractCategory,
    ContractSubtype ContractSubtype,
    ContractForm Form,
    int EmployeeId,
    int EmployerPartyId,
    int EmployerSignerUserId,
    int PositionId,
    int SalaryGradeId,
    string JobDescription,
    string WorkLocation,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string WorkingTimeJson,
    string ContractText,
    bool HasProbation,
    DateTime? ProbationStartDate,
    DateTime? ProbationEndDate,
    decimal? ProbationSalary,
    ProbationResultStatus? ProbationResultStatus,
    IReadOnlyList<ContractAllowanceInput> Allowances);
