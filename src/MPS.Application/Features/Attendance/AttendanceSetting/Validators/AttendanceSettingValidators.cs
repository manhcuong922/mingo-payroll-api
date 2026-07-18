using FluentValidation;
using MPS.Application.Features.Attendance.AttendanceSetting.Commands;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Validators;

public class UpdateAttendanceSettingCommandValidator : AbstractValidator<UpdateAttendanceSettingCommand>
{
    public UpdateAttendanceSettingCommandValidator()
    {
        RuleFor(x => x.LockAfterDays)
            .InclusiveBetween(0, 31)
            .WithMessage("LockAfterDays phải nằm trong khoảng 0-31 ngày.");

        RuleFor(x => x.LateEarlyMaxOccurrencesPerMonth)
            .InclusiveBetween(0, 31).When(x => x.LateEarlyMaxOccurrencesPerMonth.HasValue)
            .WithMessage("LateEarlyMaxOccurrencesPerMonth phải trong khoảng 0-31.");

        RuleFor(x => x.LateEarlyMaxMinutesPerOccurrence)
            .InclusiveBetween(1, 240).When(x => x.LateEarlyMaxMinutesPerOccurrence.HasValue)
            .WithMessage("LateEarlyMaxMinutesPerOccurrence phải trong khoảng 1-240 phút.");

        RuleFor(x => x.LateEarlyBackdateDaysAllowed)
            .InclusiveBetween(0, 31).When(x => x.LateEarlyBackdateDaysAllowed.HasValue)
            .WithMessage("LateEarlyBackdateDaysAllowed phải trong khoảng 0-31 ngày.");

        RuleFor(x => x.WfhMaxDaysPerMonth)
            .InclusiveBetween(0m, 31m).When(x => x.WfhMaxDaysPerMonth.HasValue)
            .WithMessage("WfhMaxDaysPerMonth phải trong khoảng 0-31 ngày.");

        RuleFor(x => x.WfhMinNoticeDays)
            .InclusiveBetween(0, 30).When(x => x.WfhMinNoticeDays.HasValue)
            .WithMessage("WfhMinNoticeDays phải trong khoảng 0-30 ngày.");

        RuleFor(x => x.WfhMaxDaysPerRequest)
            .InclusiveBetween(0.5m, 90m).When(x => x.WfhMaxDaysPerRequest.HasValue)
            .WithMessage("WfhMaxDaysPerRequest phải trong khoảng 0.5-90 ngày.");

        RuleFor(x => x.WfhWorkPlanMinLength)
            .InclusiveBetween(0, 500).When(x => x.WfhWorkPlanMinLength.HasValue)
            .WithMessage("WfhWorkPlanMinLength phải trong khoảng 0-500 ký tự.");
    }
}
