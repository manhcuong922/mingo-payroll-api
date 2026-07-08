using FluentValidation;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Validators;

/// <summary>
/// Validator cho batch OT (luồng mới).
/// </summary>
public class CreateBatchOvertimeRequestCommandValidator : AbstractValidator<CreateBatchOvertimeRequestCommand>
{
    public CreateBatchOvertimeRequestCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.OtStartTime != x.OtEndTime)
            .WithMessage("Giờ kết thúc phải khác giờ bắt đầu.");
        RuleFor(x => x.EmployeeInformationIds)
            .NotEmpty().WithMessage("Phải chọn ít nhất 1 nhân viên.");
        RuleFor(x => x.ApproverRoute).IsInEnum();
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
