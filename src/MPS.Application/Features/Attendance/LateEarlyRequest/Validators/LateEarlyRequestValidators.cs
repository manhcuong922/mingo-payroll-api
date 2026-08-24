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
        // ⭐ LATE-EARLY-2026-08 — trần trên KHÔNG còn là hằng số 240: nó là thời lượng CA ĐƯỢC PHÂN
        // của ngày, chỉ biết được sau khi tra assignment nên chặn ở
        // `CreateLateEarlyRequestCommandHandler`. Ở đây chỉ giữ sàn + chặn số vô lý (24h).
        RuleFor(x => x.Minutes)
            .InclusiveBetween((short)1, (short)1440)
            .WithMessage("Minutes phải là số phút dương và không vượt quá 1 ngày.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ApproverRoute)
            .IsInEnum().When(x => x.ApproverRoute.HasValue);
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
