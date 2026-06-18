using FluentValidation;
using MPS.Application.Features.Compliance.LegalDocument.Commands;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Validators;

public sealed class CreateLegalDocumentCommandValidator : AbstractValidator<CreateLegalDocumentCommand>
{
    public CreateLegalDocumentCommandValidator(ILegalDocumentRepository repo)
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Code).MustAsync(async (code, ct) =>
        {
            if (string.IsNullOrWhiteSpace(code)) return true;
            var existing = await repo.GetByCodeAsync(code, ct);
            return existing is null;
        }).WithMessage(x => $"LegalDocument with Code '{x.Code}' already exists.");

        RuleFor(x => x.DocumentType).Must(t => LegalDocumentTypes.All.Contains(t))
            .WithMessage($"DocumentType must be one of: {string.Join(", ", LegalDocumentTypes.All)}.");

        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IssuedBy).NotEmpty().MaximumLength(200);

        RuleFor(x => x.EffectiveDate)
            .GreaterThanOrEqualTo(x => x.IssuedDate)
            .WithMessage("EffectiveDate must be on or after IssuedDate.");

        When(x => x.ExpiryDate.HasValue, () =>
        {
            RuleFor(x => x.ExpiryDate!.Value)
                .GreaterThan(x => x.EffectiveDate)
                .WithMessage("ExpiryDate must be after EffectiveDate.");
        });

        When(x => x.Url is not null, () =>
        {
            RuleFor(x => x.Url!).MaximumLength(2000);
        });
    }
}
