using FluentValidation;
using MPS.Application.Features.ContractTemplate.Commands;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTemplate.Validators;

public class ContractTemplateCreateCommandValidator : AbstractValidator<ContractTemplateCreateCommand>
{
    public ContractTemplateCreateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        // LaborFullTime templates require Category (Definite / Indefinite). Other kinds don't
        // carry the duration concept, so their Category/Subtype should be null.
        RuleFor(x => x)
            .Must(cmd => cmd.Kind != ContractKind.LaborFullTime || cmd.ContractCategory is not null)
            .WithMessage("LaborFullTime template requires ContractCategory (Definite or Indefinite).")
            .OverridePropertyName("ContractCategory");
    }
}
