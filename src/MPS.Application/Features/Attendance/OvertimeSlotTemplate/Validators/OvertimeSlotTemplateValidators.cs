using FluentValidation;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Commands;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Common;

namespace MPS.Application.Features.Attendance.OvertimeSlotTemplate.Validators;

/// <summary>
/// Rule chung cho Create/Update: validate override multiplier &gt;= sàn luật + null-out
/// inactive side theo <see cref="IOvertimeSlotTemplateCommand.IsNightSlot"/>.
/// </summary>
internal static class OvertimeSlotTemplateCommonRules
{
    public static void ApplyCommon<T>(AbstractValidator<T> v) where T : IOvertimeSlotTemplateCommand
    {
        v.RuleFor(x => x).Must(x => x.StartTime != x.EndTime)
            .WithMessage("StartTime và EndTime phải khác nhau. Nếu EndTime sớm hơn StartTime, slot được hiểu là kết thúc ngày hôm sau.");
        v.RuleFor(x => x.Description).MaximumLength(500);

        // ── Override multiplier ≥ sàn luật (Điều 98 BLLĐ). Null = dùng sàn, cho phép. ──

        v.RuleFor(x => x.MultiplierNormalDay)
            .Must(v => v is null || v >= OvertimeMultiplierFloors.NormalDay)
            .WithMessage($"MultiplierNormalDay không được thấp hơn sàn {OvertimeMultiplierFloors.NormalDay}% (Điều 98.1.a BLLĐ).");

        v.RuleFor(x => x.MultiplierWeeklyRestDay)
            .Must(v => v is null || v >= OvertimeMultiplierFloors.WeeklyRestDay)
            .WithMessage($"MultiplierWeeklyRestDay không được thấp hơn sàn {OvertimeMultiplierFloors.WeeklyRestDay}% (Điều 98.1.b BLLĐ).");

        v.RuleFor(x => x.MultiplierHolidayDay)
            .Must(v => v is null || v >= OvertimeMultiplierFloors.HolidayDay)
            .WithMessage($"MultiplierHolidayDay không được thấp hơn sàn {OvertimeMultiplierFloors.HolidayDay}% (Điều 98.1.c BLLĐ).");

        v.RuleFor(x => x.MultiplierNormalNightNoPrevOt)
            .Must(v => v is null || v >= OvertimeMultiplierFloors.NormalNightNoPrevOt)
            .WithMessage($"MultiplierNormalNightNoPrevOt không được thấp hơn sàn {OvertimeMultiplierFloors.NormalNightNoPrevOt}% (Điều 98.3 BLLĐ).");

        v.RuleFor(x => x.MultiplierNormalNightWithPrevOt)
            .Must(v => v is null || v >= OvertimeMultiplierFloors.NormalNightWithPrevOt)
            .WithMessage($"MultiplierNormalNightWithPrevOt không được thấp hơn sàn {OvertimeMultiplierFloors.NormalNightWithPrevOt}% (Điều 98.3 BLLĐ).");

        v.RuleFor(x => x.MultiplierWeeklyRestNight)
            .Must(v => v is null || v >= OvertimeMultiplierFloors.WeeklyRestNight)
            .WithMessage($"MultiplierWeeklyRestNight không được thấp hơn sàn {OvertimeMultiplierFloors.WeeklyRestNight}% (Điều 98.3 BLLĐ).");

        v.RuleFor(x => x.MultiplierHolidayNight)
            .Must(v => v is null || v >= OvertimeMultiplierFloors.HolidayNight)
            .WithMessage($"MultiplierHolidayNight không được thấp hơn sàn {OvertimeMultiplierFloors.HolidayNight}% (Điều 98.3 BLLĐ).");
    }
}

public sealed class CreateOvertimeSlotTemplateCommandValidator : AbstractValidator<CreateOvertimeSlotTemplateCommand>
{
    public CreateOvertimeSlotTemplateCommandValidator()
    {
        OvertimeSlotTemplateCommonRules.ApplyCommon(this);
    }
}

public sealed class UpdateOvertimeSlotTemplateCommandValidator : AbstractValidator<UpdateOvertimeSlotTemplateCommand>
{
    public UpdateOvertimeSlotTemplateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        OvertimeSlotTemplateCommonRules.ApplyCommon(this);
    }
}

public sealed class DeleteOvertimeSlotTemplateCommandValidator : AbstractValidator<DeleteOvertimeSlotTemplateCommand>
{
    public DeleteOvertimeSlotTemplateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
