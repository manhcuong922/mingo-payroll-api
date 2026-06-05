using FluentValidation;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;

namespace MPS.Application.Features.Contract.Validators;

public class ReplaceContractFileCommandValidator : AbstractValidator<ReplaceContractFileCommand>
{
    public ReplaceContractFileCommandValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("FileName is required.")
            .MaximumLength(255);

        RuleFor(x => x.ContentType)
            .Equal(PdfStreamValidator.PdfContentType)
            .WithMessage($"ContentType must be '{PdfStreamValidator.PdfContentType}'.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(PdfStreamValidator.DefaultMaxSizeBytes)
                .WithMessage($"FileSizeBytes must be ≤ {PdfStreamValidator.DefaultMaxSizeBytes / 1024 / 1024} MB.");

        RuleFor(x => x.PdfStream).NotNull();

        RuleFor(x => x.SignedBySource).MaximumLength(100);
    }
}
