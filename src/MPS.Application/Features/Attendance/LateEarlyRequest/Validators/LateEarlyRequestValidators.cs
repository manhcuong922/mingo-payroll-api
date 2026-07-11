using FluentValidation;
using MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Validators;

public class CreateLateEarlyRequestCommandValidator : AbstractValidator<CreateLateEarlyRequestCommand>
{
    public CreateLateEarlyRequestCommandValidator()
    {
        RuleFor(x => x.WorkDate)
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
                    && d <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)))
            .WithMessage("WorkDate phải trong khoảng ±30 ngày so với hôm nay.");
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Minutes)
            .InclusiveBetween((short)1, (short)240)
            .WithMessage("Minutes phải trong khoảng 1-240 (nếu quá 4 giờ nên nộp Leave nửa ngày).");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ApproverRoute).IsInEnum();
        RuleFor(x => x.OverrideReason).MaximumLength(500);
    }
}

public class ApproveLateEarlyRequestCommandValidator : AbstractValidator<ApproveLateEarlyRequestCommand>
{
    public ApproveLateEarlyRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
        RuleFor(x => x.OverrideReason).MaximumLength(500);
    }
}

public class RejectLateEarlyRequestCommandValidator : AbstractValidator<RejectLateEarlyRequestCommand>
{
    public RejectLateEarlyRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class CancelLateEarlyRequestCommandValidator : AbstractValidator<CancelLateEarlyRequestCommand>
{
    public CancelLateEarlyRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CancelReason).MaximumLength(500);
    }
}
