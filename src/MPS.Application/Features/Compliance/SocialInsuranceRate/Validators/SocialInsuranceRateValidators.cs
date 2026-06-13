using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;
using MPS.Application.Options;
using Microsoft.Extensions.Options;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Validators;

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
        RuleFor(x => x.UnemploymentCapMultiplier).InclusiveBetween(1, 100);
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);

        RuleFor(x => x.EmployerOccupationalDiseasePercent)
            .LessThanOrEqualTo(x => x.EmployerSocialPercent)
            .WithMessage("EmployerOccupationalDiseasePercent must not exceed EmployerSocialPercent.");
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
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EmployerOccupationalDiseasePercent)
            .LessThanOrEqualTo(x => x.EmployerSocialPercent)
            .WithMessage("EmployerOccupationalDiseasePercent must not exceed EmployerSocialPercent.");
    }
}
