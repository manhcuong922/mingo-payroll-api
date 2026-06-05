using FluentValidation;
using MPS.Application.Features.ContractTermination.Commands;

namespace MPS.Application.Features.ContractTermination.Validators;

public class CreateContractTerminationCommandValidator : AbstractValidator<CreateContractTerminationCommand>
{
    public CreateContractTerminationCommandValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.ReasonRefId).GreaterThan(0);
        RuleFor(x => x.TerminationParty).IsInEnum();

        RuleFor(x => x.TerminationDate)
            .NotEmpty()
            .Must(d => d <= DateTime.UtcNow.AddDays(365))
                .WithMessage("TerminationDate cannot be more than 1 year in the future.");

        RuleFor(x => x.NoticeSentAt)
            .NotEmpty()
            .Must((cmd, sent) => sent <= cmd.TerminationDate)
                .WithMessage("NoticeSentAt must be on or before TerminationDate.");

        RuleFor(x => x.NoticeDaysAhead)
            .GreaterThanOrEqualTo(0).When(x => x.NoticeDaysAhead.HasValue);

        RuleFor(x => x.LegalBasis)
            .NotEmpty().WithMessage("LegalBasis is required (e.g. 'Điều 35 k.1').")
            .MaximumLength(200);

        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
