using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;
using MPS.Application.Options;
using MPS.Domain.Enums.Compliance;
using Microsoft.Extensions.Options;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Validators;

internal static class SocialInsuranceRateValidationRules
{
    public static void ValidateFunds<T>(IReadOnlyList<FundEntryInput>? funds, ValidationContext<T> ctx)
    {
        if (funds is null || funds.Count == 0) return;

        var seen = new HashSet<string>();
        foreach (var f in funds)
        {
            if (!SocialInsuranceFundCodes.All.Contains(f.FundCode))
                ctx.AddFailure("Funds.FundCode", $"FundCode must be one of: {string.Join(", ", SocialInsuranceFundCodes.All)}. Found '{f.FundCode}'.");
            if (!seen.Add(f.FundCode))
                ctx.AddFailure("Funds.FundCode", $"Duplicate FundCode: {f.FundCode}.");
            if (f.EmployeePercent < 0 || f.EmployeePercent > 100)
                ctx.AddFailure("Funds.EmployeePercent", $"Fund {f.FundCode} EmployeePercent must be in [0, 100].");
            if (f.EmployerPercent < 0 || f.EmployerPercent > 100)
                ctx.AddFailure("Funds.EmployerPercent", $"Fund {f.FundCode} EmployerPercent must be in [0, 100].");
        }
    }
}

public sealed class CreateSocialInsuranceRateCommandValidator : AbstractValidator<CreateSocialInsuranceRateCommand>
{
    public CreateSocialInsuranceRateCommandValidator(IClock clock, IOptions<ComplianceOptions> options)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var maxForward = options.Value.MaxForwardDays;

        When(x => !x.ApplyImmediately, () =>
        {
            RuleFor(x => x.ValidFrom)
                .GreaterThan(today).WithMessage($"ValidFrom must be strictly after today ({today:yyyy-MM-dd}).")
                .LessThanOrEqualTo(today.AddDays(maxForward)).WithMessage($"ValidFrom cannot be more than {maxForward} days in the future.");
        });

        RuleFor(x => x.EmployeeSocialPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerSocialPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerOccupationalDiseasePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployeeHealthPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerHealthPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployeeUnemploymentPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerUnemploymentPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerUnionFundPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.ReferenceAmount).GreaterThan(0);
        RuleFor(x => x.SocialCapMultiplier).InclusiveBetween(1, 100);
        RuleFor(x => x.HealthCapMultiplier).InclusiveBetween(1, 100);
        RuleFor(x => x.UnemploymentCapMultiplier).InclusiveBetween(1, 100);
        RuleFor(x => x.LegalReference).MaximumLength(500); // Obsolete — optional, LegalDocumentIds thay thế (BR-15)

        RuleFor(x => x.EmployerOccupationalDiseasePercent)
            .LessThanOrEqualTo(x => x.EmployerSocialPercent)
            .WithMessage("EmployerOccupationalDiseasePercent must not exceed EmployerSocialPercent.");

        RuleFor(x => x.Funds).NotEmpty().WithMessage("At least one social insurance fund is required.");
        RuleFor(x => x.Funds).Custom((funds, ctx) => SocialInsuranceRateValidationRules.ValidateFunds(funds, ctx));

        RuleFor(x => x.EligibilityMode).Must(m => SocialInsuranceEligibilityModes.All.Contains(m))
            .WithMessage($"EligibilityMode must be one of: {string.Join(", ", SocialInsuranceEligibilityModes.All)}.");

        When(x => x.EligibilityMode == SocialInsuranceEligibilityModes.ByContractSubtype, () =>
        {
            RuleFor(x => x.EligibleContractSubtypes).NotEmpty()
                .WithMessage("EligibleContractSubtypes is required when EligibilityMode is ByContractSubtype.");
            RuleFor(x => x.MinTenureMonths).Null()
                .WithMessage("MinTenureMonths must be null when EligibilityMode is ByContractSubtype.");
        });

        When(x => x.EligibilityMode == SocialInsuranceEligibilityModes.ByTenureMonths, () =>
        {
            RuleFor(x => x.MinTenureMonths).NotNull().GreaterThan(0)
                .WithMessage("MinTenureMonths is required and must be > 0 when EligibilityMode is ByTenureMonths.");
            RuleFor(x => x.EligibleContractSubtypes).Empty()
                .WithMessage("EligibleContractSubtypes must be null/empty when EligibilityMode is ByTenureMonths.");
        });

        RuleFor(x => x.LegalDocumentIds).NotEmpty()
            .WithMessage("At least one LegalDocumentId is required (BR-15).");
    }
}

public sealed class UpdateSocialInsuranceRateCommandValidator : AbstractValidator<UpdateSocialInsuranceRateCommand>
{
    public UpdateSocialInsuranceRateCommandValidator(IClock clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        RuleFor(x => x.ValidFrom).GreaterThan(today).WithMessage($"ValidFrom must be strictly after today ({today:yyyy-MM-dd}).");
        RuleFor(x => x.EmployeeSocialPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerSocialPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerOccupationalDiseasePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployeeHealthPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerHealthPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployeeUnemploymentPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerUnemploymentPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EmployerUnionFundPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.ReferenceAmount).GreaterThan(0);
        RuleFor(x => x.SocialCapMultiplier).InclusiveBetween(1, 100);
        RuleFor(x => x.HealthCapMultiplier).InclusiveBetween(1, 100);
        RuleFor(x => x.UnemploymentCapMultiplier).InclusiveBetween(1, 100);
        RuleFor(x => x.LegalReference).MaximumLength(500); // Obsolete — optional, LegalDocumentIds thay thế (BR-15)
        RuleFor(x => x.EmployerOccupationalDiseasePercent)
            .LessThanOrEqualTo(x => x.EmployerSocialPercent)
            .WithMessage("EmployerOccupationalDiseasePercent must not exceed EmployerSocialPercent.");

        RuleFor(x => x.Funds).NotEmpty().WithMessage("At least one social insurance fund is required.");
        RuleFor(x => x.Funds).Custom((funds, ctx) => SocialInsuranceRateValidationRules.ValidateFunds(funds, ctx));

        RuleFor(x => x.EligibilityMode).Must(m => SocialInsuranceEligibilityModes.All.Contains(m))
            .WithMessage($"EligibilityMode must be one of: {string.Join(", ", SocialInsuranceEligibilityModes.All)}.");

        When(x => x.EligibilityMode == SocialInsuranceEligibilityModes.ByContractSubtype, () =>
        {
            RuleFor(x => x.EligibleContractSubtypes).NotEmpty()
                .WithMessage("EligibleContractSubtypes is required when EligibilityMode is ByContractSubtype.");
            RuleFor(x => x.MinTenureMonths).Null()
                .WithMessage("MinTenureMonths must be null when EligibilityMode is ByContractSubtype.");
        });

        When(x => x.EligibilityMode == SocialInsuranceEligibilityModes.ByTenureMonths, () =>
        {
            RuleFor(x => x.MinTenureMonths).NotNull().GreaterThan(0)
                .WithMessage("MinTenureMonths is required and must be > 0 when EligibilityMode is ByTenureMonths.");
            RuleFor(x => x.EligibleContractSubtypes).Empty()
                .WithMessage("EligibleContractSubtypes must be null/empty when EligibilityMode is ByTenureMonths.");
        });

        RuleFor(x => x.LegalDocumentIds).NotEmpty()
            .WithMessage("At least one LegalDocumentId is required (BR-15).");
    }
}
