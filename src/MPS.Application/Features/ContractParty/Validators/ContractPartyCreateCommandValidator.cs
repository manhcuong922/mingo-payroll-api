using FluentValidation;
using MPS.Application.Features.ContractParty.Commands;

namespace MPS.Application.Features.ContractParty.Validators;

public class ContractPartyCreateCommandValidator : AbstractValidator<ContractPartyCreateCommand>
{
    public ContractPartyCreateCommandValidator()
    {
        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("LegalName is required.")
            .MaximumLength(300);

        RuleFor(x => x.TaxCode)
            .NotEmpty().WithMessage("TaxCode is required.")
            .MaximumLength(20)
            .Matches("^[0-9\\-]+$").WithMessage("TaxCode may only contain digits and hyphen.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500);

        RuleFor(x => x.RepresentativeFullName)
            .NotEmpty().WithMessage("RepresentativeFullName is required.")
            .MaximumLength(200);

        RuleFor(x => x.RepresentativeTitle)
            .NotEmpty().WithMessage("RepresentativeTitle is required.")
            .MaximumLength(100);
    }
}
