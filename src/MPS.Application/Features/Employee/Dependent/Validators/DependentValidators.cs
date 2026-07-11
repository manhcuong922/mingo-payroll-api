using FluentValidation;
using MPS.Application.Features.Employee.Dependent.Commands;

namespace MPS.Application.Features.Employee.Dependent.Validators;

public class CreateDependentRegistrationRequestCommandValidator : AbstractValidator<CreateDependentRegistrationRequestCommand>
{
    public CreateDependentRegistrationRequestCommandValidator()
    {
        RuleFor(x => x.RequestType).IsInEnum();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Gender).MaximumLength(20);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.SupportingDocumentUrl).MaximumLength(1000);
        RuleFor(x => x.TaxIdentificationNumber)
            .Matches(@"^\d{10}$|^\d{13}$")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxIdentificationNumber))
            .WithMessage("TaxIdentificationNumber phải đúng 10 hoặc 13 số.");
        RuleFor(x => x.CitizenId)
            .Matches(@"^\d{12}$")
            .When(x => !string.IsNullOrWhiteSpace(x.CitizenId))
            .WithMessage("CitizenId phải đúng 12 số.");
        RuleFor(x => x.DateOfBirth)
            .Must(d => d.HasValue && d.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("DateOfBirth không được ở tương lai.");
    }
}

public class ApproveDependentRegistrationRequestCommandValidator : AbstractValidator<ApproveDependentRegistrationRequestCommand>
{
    public ApproveDependentRegistrationRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class RejectDependentRegistrationRequestCommandValidator : AbstractValidator<RejectDependentRegistrationRequestCommand>
{
    public RejectDependentRegistrationRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReviewNote).MaximumLength(500);
    }
}

public class CancelDependentRegistrationRequestCommandValidator : AbstractValidator<CancelDependentRegistrationRequestCommand>
{
    public CancelDependentRegistrationRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CancelReason).MaximumLength(500);
    }
}
