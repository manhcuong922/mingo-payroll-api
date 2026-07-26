using FluentValidation;
using MPS.Application.Features.Attendance.FaceRegistration.Commands;

namespace MPS.Application.Features.Attendance.FaceRegistration.Validators;

public class SubmitFaceRegistrationCommandValidator : AbstractValidator<SubmitFaceRegistrationCommand>
{
    public SubmitFaceRegistrationCommandValidator()
    {
        RuleFor(x => x.PhotoBase64).NotEmpty();
        RuleFor(x => x.EmbeddingJson).NotEmpty();
    }
}

public class RejectFaceRegistrationCommandValidator : AbstractValidator<RejectFaceRegistrationCommand>
{
    public RejectFaceRegistrationCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
