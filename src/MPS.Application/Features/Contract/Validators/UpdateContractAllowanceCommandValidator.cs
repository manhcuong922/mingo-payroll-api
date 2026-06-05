using FluentValidation;
using MPS.Application.Features.Contract.Commands;

namespace MPS.Application.Features.Contract.Validators;

public class UpdateContractAllowanceCommandValidator : AbstractValidator<UpdateContractAllowanceCommand>
{
    public UpdateContractAllowanceCommandValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.AllowanceTypeId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency)
            .Must(c => string.IsNullOrEmpty(c) || (c.Length == 3 && c.All(char.IsLetter)))
            .WithMessage("Currency must be a 3-letter ISO 4217 code if specified.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
