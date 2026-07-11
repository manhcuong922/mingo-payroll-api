using FluentValidation;
using MPS.Application.Features.Attendance.LeaveBalance.Commands;
using MPS.Application.Features.Attendance.LeaveRequest.Commands;

namespace MPS.Application.Features.Attendance.LeaveRequest.Validators;

public class CreateLeaveRequestCommandValidator : AbstractValidator<CreateLeaveRequestCommand>
{
    public CreateLeaveRequestCommandValidator()
    {
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.SupportingDocumentUrl).MaximumLength(1000);
        RuleFor(x => x.MedicalCertificateUrl).MaximumLength(1000);
        RuleFor(x => x.InsuranceCertNumber)
            .MaximumLength(50)
            .Matches(@"^[0-9A-Za-z\-/]*$")
            .WithMessage("InsuranceCertNumber chỉ chứa chữ, số, dấu '-' hoặc '/'.");
        RuleFor(x => x.OverrideReason).MaximumLength(500);
    }
}

public class ApproveLeaveRequestCommandValidator : AbstractValidator<ApproveLeaveRequestCommand>
{
    public ApproveLeaveRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
        RuleFor(x => x.MedicalCertificateUrl).MaximumLength(1000);
        RuleFor(x => x.InsuranceCertNumber).MaximumLength(50);
        RuleFor(x => x.OverrideReason).MaximumLength(500);
    }
}

public class RejectLeaveRequestCommandValidator : AbstractValidator<RejectLeaveRequestCommand>
{
    public RejectLeaveRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class CancelLeaveRequestCommandValidator : AbstractValidator<CancelLeaveRequestCommand>
{
    public CancelLeaveRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CancelReason).MaximumLength(500);
    }
}

public class InitLeaveBalanceCommandValidator : AbstractValidator<InitLeaveBalanceCommand>
{
    public InitLeaveBalanceCommandValidator()
    {
        RuleFor(x => x.EmployeeInformationId).GreaterThan(0);
        RuleFor(x => x.Year).InclusiveBetween(2000, 9999);
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
    }
}
