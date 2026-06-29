using FluentValidation;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.ShiftTemplate.Commands;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Validators;

public class CreateShiftTemplateCommandValidator : AbstractValidator<CreateShiftTemplateCommand>
{
    public CreateShiftTemplateCommandValidator()
    {
        Include(new ShiftTemplateBaseValidator());
        RuleFor(x => x.AppliedAttendanceRuleId)
            .GreaterThan(0)
            .WithMessage("AppliedAttendanceRuleId is required.");
    }
}

public class UpdateShiftTemplateCommandValidator : AbstractValidator<UpdateShiftTemplateCommand>
{
    public UpdateShiftTemplateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AppliedAttendanceRuleId)
            .GreaterThan(0)
            .WithMessage("AppliedAttendanceRuleId is required.");
        Include(new ShiftTemplateBaseValidator());
    }
}

internal sealed class ShiftTemplateBaseValidator : AbstractValidator<IShiftTemplateCommand>
{
    public ShiftTemplateBaseValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(20)
            .Matches("^[A-Z0-9-]{2,20}$")
            .WithMessage("Code must be 2-20 chars and contain only uppercase letters, digits, and hyphens.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime)
            .WithMessage("EndTime must be greater than StartTime.");

        RuleFor(x => x.StandardHours)
            .InclusiveBetween(0.5m, RegularHoursCap.MaxPerDay)
            .WithMessage(
                $"StandardHours phải trong khoảng 0.5..{RegularHoursCap.MaxPerDay}h — Điều 105.1 BLLĐ giới hạn ≤ 8h/ngày khi áp mode Daily.");
        RuleFor(x => x.StandardDays).InclusiveBetween(0.25m, 1.00m);
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x)
            .Must(x => x.BreakStartTime.HasValue == x.BreakEndTime.HasValue)
            .WithMessage("BreakStartTime and BreakEndTime must be set together.");

        RuleFor(x => x)
            .Must(HasValidBreakRange)
            .WithMessage("Break time must be within the shift range, and BreakEndTime must be greater than BreakStartTime.");

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

        return breakEnd > breakStart
            && breakStart >= x.StartTime
            && breakStart <= x.EndTime
            && breakEnd >= x.StartTime
            && breakEnd <= x.EndTime;
    }

    private static decimal NetHours(IShiftTemplateCommand x)
    {
        var netHours = (decimal)(x.EndTime - x.StartTime).TotalHours;
        if (x.BreakStartTime.HasValue && x.BreakEndTime.HasValue)
            netHours -= (decimal)(x.BreakEndTime.Value - x.BreakStartTime.Value).TotalHours;
        return netHours;
    }
}
