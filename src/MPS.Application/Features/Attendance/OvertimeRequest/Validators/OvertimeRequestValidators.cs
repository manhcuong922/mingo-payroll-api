using FluentValidation;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Validators;

/// <summary>
/// Validator cho NV self-create OT (luồng v3). Chi tiết business rules (đơn active,
/// task InProgress, cap, overlap) validate ở handler — validator chỉ check shape.
/// </summary>
public class CreateOvertimeRequestCommandValidator : AbstractValidator<CreateOvertimeRequestCommand>
{
    public CreateOvertimeRequestCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0)
            .WithMessage("Phải chọn đơn sản xuất.");
        RuleFor(x => x)
            .Must(x => x.OtStartTime != x.OtEndTime)
            .WithMessage("Giờ kết thúc phải khác giờ bắt đầu.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>
/// Validator cho PM Review (Forward | Reject). Rule mutually exclusive:
/// Forward yêu cầu ApproverRoute non-null, Reject yêu cầu null.
/// </summary>
public class PmReviewOvertimeRequestCommandValidator : AbstractValidator<PmReviewOvertimeRequestCommand>
{
    public PmReviewOvertimeRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0);
        RuleFor(x => x.Decision).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);

        When(x => x.Decision == PmReviewDecision.Forward, () =>
        {
            RuleFor(x => x.ApproverRoute)
                .NotNull().WithMessage("Khi Forward, phải chọn route duyệt cuối (HR hoặc DM).")
                .IsInEnum();
        });

        When(x => x.Decision == PmReviewDecision.Reject, () =>
        {
            RuleFor(x => x.ApproverRoute)
                .Null().WithMessage("Khi Reject, không được set route duyệt cuối.");
        });
    }
}

public class ApproveOvertimeRequestCommandValidator : AbstractValidator<ApproveOvertimeRequestCommand>
{
    public ApproveOvertimeRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class RejectOvertimeRequestCommandValidator : AbstractValidator<RejectOvertimeRequestCommand>
{
    public RejectOvertimeRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}
