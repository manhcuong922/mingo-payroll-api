using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.LaborLawConstants.Commands;
using MPS.Application.Options;
using Microsoft.Extensions.Options;

namespace MPS.Application.Features.Compliance.LaborLawConstants.Validators;

public sealed class CreateLaborLawConstantsCommandValidator : AbstractValidator<CreateLaborLawConstantsCommand>
{
    public CreateLaborLawConstantsCommandValidator(IClock clock, IOptions<ComplianceOptions> options)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var maxForward = options.Value.MaxForwardDays;

        When(x => !x.ApplyImmediately, () =>
        {
            RuleFor(x => x.ValidFrom)
                .GreaterThan(today).WithMessage($"ValidFrom must be strictly after today ({today:yyyy-MM-dd}).")
                .LessThanOrEqualTo(today.AddDays(maxForward));
        });

        RuleFor(x => x.ProbationSalaryMinPercent).InclusiveBetween(0.01m, 100m)
            .WithMessage("ProbationSalaryMinPercent must be in range (0, 100]. Unit: percent (e.g., 85 = 85%).");
        RuleFor(x => x.NightShiftSurchargePercent).InclusiveBetween(0.01m, 100m)
            .WithMessage("NightShiftSurchargePercent must be in range (0, 100]. Unit: percent.");
        RuleFor(x => x.StandardWorkingHoursPerDay).InclusiveBetween(0.01m, 24m);
        RuleFor(x => x.StandardWorkingDaysPerMonth).InclusiveBetween(0.01m, 31m);
        RuleFor(x => x.AnnualLeaveMinDays).GreaterThanOrEqualTo(1);
        RuleFor(x => x.OvertimeMaxHoursPerDay).InclusiveBetween(0.01m, 24m);
        RuleFor(x => x.OvertimeMaxHoursPerYear).GreaterThanOrEqualTo(1);
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);
    }
}

public sealed class UpdateLaborLawConstantsCommandValidator : AbstractValidator<UpdateLaborLawConstantsCommand>
{
    public UpdateLaborLawConstantsCommandValidator(IClock clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        RuleFor(x => x.ValidFrom).GreaterThan(today);
        RuleFor(x => x.ProbationSalaryMinPercent).InclusiveBetween(0.01m, 100m);
        RuleFor(x => x.NightShiftSurchargePercent).InclusiveBetween(0.01m, 100m);
        RuleFor(x => x.StandardWorkingHoursPerDay).InclusiveBetween(0.01m, 24m);
        RuleFor(x => x.StandardWorkingDaysPerMonth).InclusiveBetween(0.01m, 31m);
        RuleFor(x => x.AnnualLeaveMinDays).GreaterThanOrEqualTo(1);
        RuleFor(x => x.OvertimeMaxHoursPerDay).InclusiveBetween(0.01m, 24m);
        RuleFor(x => x.OvertimeMaxHoursPerYear).GreaterThanOrEqualTo(1);
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);
    }
}
