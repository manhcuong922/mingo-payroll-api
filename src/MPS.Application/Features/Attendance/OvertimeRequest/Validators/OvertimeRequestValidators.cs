using FluentValidation;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Validators;

public class CreateOvertimeRequestCommandValidator : AbstractValidator<CreateOvertimeRequestCommand>
{
    public CreateOvertimeRequestCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.OtStartTime != x.OtEndTime)
            .WithMessage("OtEndTime must be different from OtStartTime.");
        RuleFor(x => x.Reason).MaximumLength(500);
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
