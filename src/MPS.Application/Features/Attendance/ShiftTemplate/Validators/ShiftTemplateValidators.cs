using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.ShiftTemplate.Commands;
using MPS.Application.Features.Attendance.ShiftTemplate.Common;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Validators;

public class CreateShiftTemplateCommandValidator : AbstractValidator<CreateShiftTemplateCommand>
{
    public CreateShiftTemplateCommandValidator(IComplianceLookupService complianceLookup)
    {
        Include(new ShiftTemplateBaseValidator(complianceLookup));
        RuleFor(x => x.AppliedAttendanceRuleId)
            .GreaterThan(0)
            .WithMessage("AppliedAttendanceRuleId is required.");
    }
}

public class UpdateShiftTemplateCommandValidator : AbstractValidator<UpdateShiftTemplateCommand>
{
    public UpdateShiftTemplateCommandValidator(IComplianceLookupService complianceLookup)
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AppliedAttendanceRuleId)
            .GreaterThan(0)
            .WithMessage("AppliedAttendanceRuleId is required.");
        Include(new ShiftTemplateBaseValidator(complianceLookup));
    }
}

internal sealed class ShiftTemplateBaseValidator : AbstractValidator<IShiftTemplateCommand>
{
    public ShiftTemplateBaseValidator(IComplianceLookupService complianceLookup)
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(20)
            .Matches("^[A-Z0-9-]{2,20}$")
            .WithMessage("Code must be 2-20 chars and contain only uppercase letters, digits, and hyphens.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x)
            .Must(x => ShiftTimeCalculator.HasValidRange(x.StartTime, x.EndTime))
            .WithMessage("StartTime and EndTime must be different. If EndTime is earlier than StartTime, the shift is treated as ending on the next day.");

        RuleFor(x => x.StandardHours)
            .GreaterThanOrEqualTo(0.5m)
            .WithMessage("StandardHours phải ≥ 0.5h.");

        RuleFor(x => x.StandardHours)
            .CustomAsync(async (hours, ctx, ct) =>
            {
                var laborLaw = await complianceLookup.GetActiveLaborLawConstantsAsync(ct);
                if (hours > laborLaw.StandardWorkingHoursPerDay)
                    ctx.AddFailure(
                        nameof(IShiftTemplateCommand.StandardHours),
                        $"StandardHours ({hours}) vượt cap {laborLaw.StandardWorkingHoursPerDay}h/ngày — Điều 105.1 BLLĐ giới hạn ≤ {laborLaw.StandardWorkingHoursPerDay}h/ngày khi áp mode Daily.");
            });

        RuleFor(x => x.StandardDays).InclusiveBetween(0.25m, 1.00m);
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x)
            .Must(x => x.BreakStartTime.HasValue == x.BreakEndTime.HasValue)
            .WithMessage("BreakStartTime and BreakEndTime must be set together.");

        RuleFor(x => x)
            .Must(HasValidBreakRange)
            .WithMessage("Break time must be within the shift range.");

        RuleFor(x => x)
            .Must(x => x.StandardHours <= NetHours(x))
            .WithMessage("StandardHours cannot exceed net shift hours.");
    }

    private static bool HasValidBreakRange(IShiftTemplateCommand x)
    {
        if (!x.BreakStartTime.HasValue && !x.BreakEndTime.HasValue)
            return true;

        if (!x.BreakStartTime.HasValue || !x.BreakEndTime.HasValue)
            return false;

        var breakStart = x.BreakStartTime.Value;
        var breakEnd = x.BreakEndTime.Value;

        return ShiftTimeCalculator.HasValidBreakRange(
            x.StartTime,
            x.EndTime,
            breakStart,
            breakEnd);
    }

    private static decimal NetHours(IShiftTemplateCommand x)
    {
        return ShiftTimeCalculator.NetHours(
            x.StartTime,
            x.EndTime,
            x.BreakStartTime,
            x.BreakEndTime);
    }
}
