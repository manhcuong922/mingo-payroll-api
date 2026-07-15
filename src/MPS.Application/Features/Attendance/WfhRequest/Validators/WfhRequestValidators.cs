using FluentValidation;
using MPS.Application.Features.Attendance.WfhRequest.Commands;

namespace MPS.Application.Features.Attendance.WfhRequest.Validators;

public class CreateWfhRequestCommandValidator : AbstractValidator<CreateWfhRequestCommand>
{
    public CreateWfhRequestCommandValidator()
    {
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.WorkPlan)
            .NotEmpty()
            .MaximumLength(2000);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.ApproverRoute)
            .IsInEnum().When(x => x.ApproverRoute.HasValue);
        RuleFor(x => x.OverrideReason).MaximumLength(500);
    }
}

public class ApproveWfhRequestCommandValidator : AbstractValidator<ApproveWfhRequestCommand>
{
    public ApproveWfhRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
        RuleFor(x => x.OverrideReason).MaximumLength(500);
    }
}

public class RejectWfhRequestCommandValidator : AbstractValidator<RejectWfhRequestCommand>
{
    public RejectWfhRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class CancelWfhRequestCommandValidator : AbstractValidator<CancelWfhRequestCommand>
{
    public CancelWfhRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CancelReason).MaximumLength(500);
    }
}
