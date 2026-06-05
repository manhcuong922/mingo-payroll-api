using FluentValidation;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;

namespace MPS.Application.Features.Contract.Validators;

public class ActivateContractCommandValidator : AbstractValidator<ActivateContractCommand>
{
    public ActivateContractCommandValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("FileName is required.")
            .MaximumLength(255);

        RuleFor(x => x.ContentType)
            .Equal(PdfStreamValidator.PdfContentType)
            .WithMessage($"ContentType must be '{PdfStreamValidator.PdfContentType}'.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0).WithMessage("FileSizeBytes must be > 0.")
            .LessThanOrEqualTo(PdfStreamValidator.DefaultMaxSizeBytes)
                .WithMessage($"FileSizeBytes must be ≤ {PdfStreamValidator.DefaultMaxSizeBytes / 1024 / 1024} MB.");

        RuleFor(x => x.PdfStream).NotNull();

        RuleFor(x => x.SignedAt)
            .NotEmpty()
            .Must(d => d <= DateTime.UtcNow.AddMinutes(5))
                .WithMessage("SignedAt cannot be in the future.");

        RuleFor(x => x.SignedPlace)
            .NotEmpty().WithMessage("SignedPlace is required.")
            .MaximumLength(200);

        RuleFor(x => x.SignedBySource).MaximumLength(100);
    }
}
