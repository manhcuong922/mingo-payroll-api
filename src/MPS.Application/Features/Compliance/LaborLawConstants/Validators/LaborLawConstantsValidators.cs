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
        RuleFor(x => x.OvertimeNightExtraPercent).InclusiveBetween(0.01m, 100m)
            .WithMessage("OvertimeNightExtraPercent must be in range (0, 100]. Unit: percent.");
        RuleFor(x => x.NightWorkSurchargePercent).InclusiveBetween(0.01m, 100m)
            .WithMessage("NightWorkSurchargePercent must be in range (0, 100]. Unit: percent.");
        RuleFor(x => x.OvertimeNormalUpliftPercent).InclusiveBetween(0.01m, 1000m)
            .WithMessage("OvertimeNormalUpliftPercent must be in range (0, 1000]. Unit: percent (Điều 98.1.a — floor 50 = +50%).");
        RuleFor(x => x.NightEndTime).NotEqual(x => x.NightStartTime)
            .WithMessage("NightStartTime and NightEndTime must be different.");
        RuleFor(x => x.StandardWorkingHoursPerDay).InclusiveBetween(0.01m, 24m);
        RuleFor(x => x.MaxRegularHoursPerWeek).InclusiveBetween(0.01m, 168m);
        RuleFor(x => x.StandardWorkingDaysPerMonth).InclusiveBetween(0.01m, 31m);
        RuleFor(x => x.AnnualLeaveMinDays).GreaterThanOrEqualTo(1);
        RuleFor(x => x.WorkRateNormalFloor).InclusiveBetween(0.01m, 10m);
        RuleFor(x => x.WorkRateWeeklyRestFloor).InclusiveBetween(0.01m, 10m);
        RuleFor(x => x.WorkRateHolidayFloor).InclusiveBetween(0.01m, 10m);
        RuleFor(x => x.OvertimeMaxHoursPerDay).InclusiveBetween(0.01m, 24m);
        RuleFor(x => x.OvertimeMaxHoursPerMonth).InclusiveBetween(0.01m, 744m);
        RuleFor(x => x.OvertimeMaxHoursPerYear).GreaterThanOrEqualTo(1);
        RuleFor(x => x.LegalReference).MaximumLength(500); // Obsolete — optional, LegalDocumentIds thay thế (BR-15)
        RuleFor(x => x.LegalDocumentIds).NotEmpty().WithMessage("At least one LegalDocumentId is required (BR-15).");
    }
}

public sealed class UpdateLaborLawConstantsCommandValidator : AbstractValidator<UpdateLaborLawConstantsCommand>
{
    public UpdateLaborLawConstantsCommandValidator(IClock clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        RuleFor(x => x.ValidFrom).GreaterThan(today);
        RuleFor(x => x.ProbationSalaryMinPercent).InclusiveBetween(0.01m, 100m);
        RuleFor(x => x.OvertimeNightExtraPercent).InclusiveBetween(0.01m, 100m);
        RuleFor(x => x.NightWorkSurchargePercent).InclusiveBetween(0.01m, 100m);
        RuleFor(x => x.OvertimeNormalUpliftPercent).InclusiveBetween(0.01m, 1000m);
        RuleFor(x => x.NightEndTime).NotEqual(x => x.NightStartTime);
        RuleFor(x => x.StandardWorkingHoursPerDay).InclusiveBetween(0.01m, 24m);
        RuleFor(x => x.MaxRegularHoursPerWeek).InclusiveBetween(0.01m, 168m);
        RuleFor(x => x.StandardWorkingDaysPerMonth).InclusiveBetween(0.01m, 31m);
        RuleFor(x => x.AnnualLeaveMinDays).GreaterThanOrEqualTo(1);
        RuleFor(x => x.WorkRateNormalFloor).InclusiveBetween(0.01m, 10m);
        RuleFor(x => x.WorkRateWeeklyRestFloor).InclusiveBetween(0.01m, 10m);
        RuleFor(x => x.WorkRateHolidayFloor).InclusiveBetween(0.01m, 10m);
        RuleFor(x => x.OvertimeMaxHoursPerDay).InclusiveBetween(0.01m, 24m);
        RuleFor(x => x.OvertimeMaxHoursPerMonth).InclusiveBetween(0.01m, 744m);
        RuleFor(x => x.OvertimeMaxHoursPerYear).GreaterThanOrEqualTo(1);
        RuleFor(x => x.LegalReference).MaximumLength(500); // Obsolete — optional, LegalDocumentIds thay thế (BR-15)
        RuleFor(x => x.LegalDocumentIds).NotEmpty().WithMessage("At least one LegalDocumentId is required (BR-15).");
    }
}
