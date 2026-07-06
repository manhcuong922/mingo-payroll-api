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
    }
}
