using FluentValidation;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.ContractTermination.Commands;

namespace MPS.Application.Features.ContractTermination.Validators;

public class UploadTerminationNoticeCommandValidator : AbstractValidator<UploadTerminationNoticeCommand>
{
    public UploadTerminationNoticeCommandValidator()
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
