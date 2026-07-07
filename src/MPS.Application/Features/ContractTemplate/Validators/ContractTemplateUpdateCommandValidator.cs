using FluentValidation;
using MPS.Application.Features.ContractTemplate.Commands;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTemplate.Validators;

public class ContractTemplateUpdateCommandValidator : AbstractValidator<ContractTemplateUpdateCommand>
{
    public ContractTemplateUpdateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x)
            .Must(cmd => cmd.Kind != ContractKind.LaborFullTime || cmd.ContractCategory is not null)
            .WithMessage("LaborFullTime template requires ContractCategory (Definite or Indefinite).")
            .OverridePropertyName("ContractCategory");
    }
}
