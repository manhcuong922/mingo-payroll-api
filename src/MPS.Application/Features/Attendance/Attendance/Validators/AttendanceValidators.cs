using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Attendance.Commands;
using MPS.Application.Features.Attendance.Attendance.Common;

namespace MPS.Application.Features.Attendance.Attendance.Validators;

public class RecordAttendanceCommandValidator : AbstractValidator<RecordAttendanceCommand>
{
    public RecordAttendanceCommandValidator(IClock clock)
    {
        RuleFor(x => x.EmployeeInformationId).GreaterThan(0);
        RuleFor(x => x.ShiftTemplateId).GreaterThan(0).WithMessage("ShiftTemplateId là bắt buộc.");
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x).Must(c => c.CheckInTime is null || c.CheckOutTime is null || c.CheckInTime <= c.CheckOutTime)
            .WithMessage("CheckOutTime must be on/after CheckInTime.");

        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            var error = AttendanceTimeRules.ValidateNotInFuture(
                cmd.WorkDate, cmd.CheckInTime, cmd.CheckOutTime, clock.UtcNow);
            if (error is not null)
                ctx.AddFailure(nameof(cmd.WorkDate), error);
        });
    }
}

public class UpdateAttendanceCommandValidator : AbstractValidator<UpdateAttendanceCommand>
{
    public UpdateAttendanceCommandValidator(IClock clock)
    {
        RuleFor(x => x.EmployeeInformationId).GreaterThan(0);
        RuleFor(x => x.ShiftTemplateId).GreaterThan(0).WithMessage("ShiftTemplateId là bắt buộc.");
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.OverrideReason).MaximumLength(1000);
        RuleFor(x => x).Must(c => c.CheckInTime is null || c.CheckOutTime is null || c.CheckInTime <= c.CheckOutTime)
            .WithMessage("CheckOutTime must be on/after CheckInTime.");

        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            var error = AttendanceTimeRules.ValidateNotInFuture(
                cmd.WorkDate, cmd.CheckInTime, cmd.CheckOutTime, clock.UtcNow);
            if (error is not null)
                ctx.AddFailure(nameof(cmd.WorkDate), error);
        });
    }
}

public class BulkRecordAttendanceCommandValidator : AbstractValidator<BulkRecordAttendanceCommand>
{
    public BulkRecordAttendanceCommandValidator(IClock clock)
    {
        RuleFor(x => x.Records).NotEmpty();

        // WorkDate ở batch level — fail nhanh trước khi loop từng row.
        RuleFor(x => x.WorkDate).Must(d => !AttendanceTimeRules.IsWorkDateInFuture(d, clock.UtcNow))
            .WithMessage(d => $"WorkDate {d.WorkDate:yyyy-MM-dd} is in the future; attendance can only be recorded for past or current day.");

        // Per-item: ShiftTemplateId bắt buộc + CheckIn/CheckOut order.
        // Future-time của từng record check trong handler để fail per-row (không fail toàn batch).
        RuleForEach(x => x.Records).ChildRules(items =>
        {
            items.RuleFor(r => r.EmployeeInformationId).GreaterThan(0);
            items.RuleFor(r => r.ShiftTemplateId).GreaterThan(0).WithMessage("ShiftTemplateId là bắt buộc.");
            items.RuleFor(r => r).Must(r => r.CheckInTime is null || r.CheckOutTime is null || r.CheckInTime <= r.CheckOutTime)
                .WithMessage("CheckOutTime must be on/after CheckInTime.");
            items.RuleFor(r => r.Notes).MaximumLength(500);
            items.RuleFor(r => r.OverrideReason).MaximumLength(1000);
        });
    }
}
