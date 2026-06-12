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

        RuleFor(x => x)
            .Must(cmd => !(cmd.ContractCategory == ContractCategory.Indefinite && cmd.ContractSubtype == ContractSubtype.Probation))
            .WithMessage("Combination Category=Indefinite + Subtype=Probation is not allowed (Điều 25 BLLĐ 2019).");
    }
}
