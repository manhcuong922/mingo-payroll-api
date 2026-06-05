using FluentValidation;
using MPS.Application.Features.ContractAppendix.Commands;

namespace MPS.Application.Features.ContractAppendix.Validators;

public class CreateContractAppendixCommandValidator : AbstractValidator<CreateContractAppendixCommand>
{
    public CreateContractAppendixCommandValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);

        RuleFor(x => x.AppendixNo)
            .NotEmpty().WithMessage("AppendixNo is required.")
            .MaximumLength(50);

        RuleFor(x => x.AppendixType).IsInEnum();

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300);

        RuleFor(x => x.ChangedClausesJson)
            .NotEmpty().WithMessage("ChangedClausesJson is required.");

        RuleFor(x => x.AppendixText)
            .NotEmpty().WithMessage("AppendixText is required.");

        // Tuyệt đối cấm EffectiveFrom trong quá khứ — không thể có hiệu lực
        // hồi tố (phụ lục phải báo trước, Điều 33).
        RuleFor(x => x.EffectiveFrom)
            .Must(d => d.Date >= DateTime.UtcNow.Date)
                .WithMessage("EffectiveFrom must be today or later (Điều 33 — không hồi tố).");
    }
}
