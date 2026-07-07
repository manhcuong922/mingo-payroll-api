using FluentValidation;
using MPS.Application.Features.Contract.Commands;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.Contract.Validators;

/// <summary>
/// Stateless validation rules shared by Create and Update commands.
/// Pure: no repository / DB dependency (handler-level checks live in ContractPreflightChecker).
/// Rules are keyed off <see cref="ContractKind"/> and its metadata; labor-code specific rules
/// only fire for labor kinds, civil-code contracts follow a lighter ruleset.
/// </summary>
internal static class ContractValidationRules
{
    public static void ApplyCommonRules<TCommand>(AbstractValidator<TCommand> v, Func<TCommand, ContractCore> selector)
        where TCommand : class
    {
        v.RuleFor(x => selector(x).JobDescription)
            .NotEmpty().WithMessage("JobDescription is required.");

        v.RuleFor(x => selector(x).WorkLocation)
            .NotEmpty().WithMessage("WorkLocation is required.")
            .MaximumLength(500);

        v.RuleFor(x => selector(x).WorkingTimeJson)
            .NotEmpty().WithMessage("WorkingTimeJson is required.");

        v.RuleFor(x => selector(x).EmployeeInformationId).GreaterThan(0);
        v.RuleFor(x => selector(x).EmployerPartyId).GreaterThan(0);
        v.RuleFor(x => selector(x).EmployerSignerUserId)
            .GreaterThan(0)
            .When(x => selector(x).EmployerSignerUserId.HasValue)
            .WithMessage("EmployerSignerUserId must be greater than 0 when provided.");
        v.RuleFor(x => selector(x).PositionId).GreaterThan(0);
        v.RuleFor(x => selector(x).SalaryGradeId).GreaterThan(0);

        v.RuleFor(x => selector(x).EffectiveFrom).NotEmpty();

        v.RuleFor(x => selector(x))
            .Must(c => c.EffectiveTo is null || c.EffectiveFrom < c.EffectiveTo)
            .WithMessage("EffectiveFrom must be earlier than EffectiveTo.")
            .OverridePropertyName("EffectiveFrom");

        // === LaborFullTime-specific: Điều 20 constraints ===
        // Definite ⇒ EffectiveTo required AND months ≤ 36 (Điều 20 1b)
        v.RuleFor(x => selector(x))
            .Must(c => !IsLaborFullTimeDefinite(c) || c.EffectiveTo is not null)
            .WithMessage("Definite LaborFullTime contracts must have EffectiveTo. (Điều 20 BLLĐ 2019)")
            .OverridePropertyName("EffectiveTo");

        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (!IsLaborFullTimeDefinite(c) || c.EffectiveTo is null) return true;
                var months = MonthsBetween(c.EffectiveFrom, c.EffectiveTo.Value);
                return months <= LaborLawConstants.ContractDuration.DefiniteTermMaxMonths;
            })
            .WithMessage($"Definite LaborFullTime duration must not exceed {LaborLawConstants.ContractDuration.DefiniteTermMaxMonths} months. (Điều 20 1b BLLĐ 2019)")
            .OverridePropertyName("EffectiveTo");

        // Indefinite ⇒ EffectiveTo null (LaborFullTime only)
        v.RuleFor(x => selector(x))
            .Must(c => c.Kind != ContractKind.LaborFullTime
                       || c.ContractCategory != ContractCategory.Indefinite
                       || c.EffectiveTo is null)
            .WithMessage("Indefinite LaborFullTime contracts must NOT have EffectiveTo.")
            .OverridePropertyName("EffectiveTo");

        // LaborFullTime requires Category to be set (Definite or Indefinite)
        v.RuleFor(x => selector(x))
            .Must(c => c.Kind != ContractKind.LaborFullTime || c.ContractCategory is not null)
            .WithMessage("LaborFullTime requires ContractCategory (Definite or Indefinite).")
            .OverridePropertyName("ContractCategory");

        // === Kind-specific: EffectiveTo required for kinds that must have an end date ===
        v.RuleFor(x => selector(x))
            .Must(c => !ContractKindMetadata.Get(c.Kind).RequiresEffectiveTo || c.EffectiveTo is not null)
            .WithMessage(x => $"{ContractKindMetadata.Get(selector(x).Kind).DisplayNameVi} yêu cầu EffectiveTo (thời hạn).")
            .OverridePropertyName("EffectiveTo");

        // === Kind == Probation ⇒ ContractSubtype = Probation (legacy compat) ===
        v.RuleFor(x => selector(x))
            .Must(c => c.Kind != ContractKind.Probation
                       || c.ContractSubtype is null or ContractSubtype.Probation)
            .WithMessage("Probation kind is inconsistent with ContractSubtype.")
            .OverridePropertyName("ContractSubtype");

        // === Probation rules (only for probation-eligible labor contracts) ===
        v.RuleFor(x => selector(x))
            .Must(c => !c.HasProbation || c.Kind is ContractKind.LaborFullTime or ContractKind.LaborPartTime or ContractKind.Probation)
            .WithMessage("HasProbation is only allowed for labor kinds.")
            .OverridePropertyName("HasProbation");

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

        // HasProbation ⇒ contract duration ≥ 30 ngày (Điều 24 k.3) — chỉ áp dụng cho HĐLĐ có EffectiveTo
        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (!c.HasProbation) return true;
                if (c.EffectiveTo is null) return true;
                return (c.EffectiveTo.Value - c.EffectiveFrom).TotalDays >= LaborLawConstants.Probation.NotAllowedBelowDays;
            })
            .WithMessage($"Probation is not allowed for contracts shorter than {LaborLawConstants.Probation.NotAllowedBelowDays} days (Điều 24 k.3 BLLĐ 2019).")
            .OverridePropertyName("HasProbation");

        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (!c.HasProbation || !c.ProbationStartDate.HasValue || !c.ProbationEndDate.HasValue) return true;
                var days = (c.ProbationEndDate.Value - c.ProbationStartDate.Value).TotalDays;
                return days > 0 && days <= LaborLawConstants.Probation.MaxDaysEnterpriseManager;
            })
            .WithMessage($"Probation period must be > 0 and ≤ {LaborLawConstants.Probation.MaxDaysEnterpriseManager} days (Điều 25 BLLĐ 2019).")
            .OverridePropertyName("ProbationEndDate");

        // === Insurance / TaxPolicy rules by InsuranceDeterminationMode ===
        // RuleDriven: 3 BH flags + TaxPolicy must not be provided (server sets from metadata).
        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (ContractKindMetadata.Get(c.Kind).InsuranceMode != InsuranceDeterminationMode.RuleDriven) return true;
                return c.SocialInsuranceFlag is null
                       && c.HealthInsuranceFlag is null
                       && c.UnemploymentInsuranceFlag is null
                       && c.TaxPolicy is null;
            })
            .WithMessage(x =>
                $"{ContractKindMetadata.Get(selector(x).Kind).DisplayNameVi} sử dụng chế độ RuleDriven — " +
                "không được gửi SocialInsuranceFlag/HealthInsuranceFlag/UnemploymentInsuranceFlag/TaxPolicy trong request.")
            .OverridePropertyName("SocialInsuranceFlag");

        // UserChoice: all 4 must be provided.
        v.RuleFor(x => selector(x))
            .Must(c =>
            {
                if (ContractKindMetadata.Get(c.Kind).InsuranceMode != InsuranceDeterminationMode.UserChoice) return true;
                return c.SocialInsuranceFlag.HasValue
                       && c.HealthInsuranceFlag.HasValue
                       && c.UnemploymentInsuranceFlag.HasValue
                       && c.TaxPolicy.HasValue;
            })
            .WithMessage(x =>
                $"{ContractKindMetadata.Get(selector(x).Kind).DisplayNameVi} yêu cầu cung cấp đủ " +
                "SocialInsuranceFlag, HealthInsuranceFlag, UnemploymentInsuranceFlag và TaxPolicy.")
            .OverridePropertyName("SocialInsuranceFlag");

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

    private static bool IsLaborFullTimeDefinite(ContractCore c) =>
        c.Kind == ContractKind.LaborFullTime && c.ContractCategory == ContractCategory.Definite;

    private static int MonthsBetween(DateTime from, DateTime to)
    {
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        if (to.Day < from.Day) months--;
        return Math.Max(0, months);
    }
}

/// <summary>Adapter exposing the subset of fields shared by Create/Update commands to the rules above.</summary>
internal sealed record ContractCore(
    ContractKind Kind,
    ContractCategory? ContractCategory,
    ContractSubtype? ContractSubtype,
    int EmployeeInformationId,
    int EmployerPartyId,
    int? EmployerSignerUserId,
    int PositionId,
    int SalaryGradeId,
    string JobDescription,
    string WorkLocation,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string WorkingTimeJson,
    bool? SocialInsuranceFlag,
    bool? HealthInsuranceFlag,
    bool? UnemploymentInsuranceFlag,
    TaxPolicy? TaxPolicy,
    bool HasProbation,
    DateTime? ProbationStartDate,
    DateTime? ProbationEndDate,
    decimal? ProbationSalary,
    ProbationResultStatus? ProbationResultStatus,
    IReadOnlyList<ContractAllowanceInput> Allowances);
