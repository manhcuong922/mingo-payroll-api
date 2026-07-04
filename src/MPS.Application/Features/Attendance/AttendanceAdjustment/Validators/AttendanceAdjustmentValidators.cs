using FluentValidation;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Validators;

public class CreateAttendanceAdjustmentCommandValidator : AbstractValidator<CreateAttendanceAdjustmentCommand>
{
    public CreateAttendanceAdjustmentCommandValidator()
    {
        RuleFor(x => x.AttendanceRecordId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x).Must(c => AttendanceTimeRules.HasValidObservedRange(c.NewCheckInAt, c.NewCheckOutAt))
            .WithMessage("NewCheckOutAt must be later than NewCheckInAt.");
    }
}

public class OverrideAttendanceCommandValidator : AbstractValidator<OverrideAttendanceCommand>
{
    public OverrideAttendanceCommandValidator()
    {
        RuleFor(x => x.EmployeeInformationId).GreaterThan(0);
        RuleFor(x => x.OverrideReason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x).Must(c => AttendanceTimeRules.HasValidObservedRange(c.NewCheckInAt, c.NewCheckOutAt))
            .WithMessage("NewCheckOutAt must be later than NewCheckInAt.");
    }
}

public class ApproveAttendanceAdjustmentCommandValidator : AbstractValidator<ApproveAttendanceAdjustmentCommand>
{
    public ApproveAttendanceAdjustmentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class RejectAttendanceAdjustmentCommandValidator : AbstractValidator<RejectAttendanceAdjustmentCommand>
{
    public RejectAttendanceAdjustmentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}
