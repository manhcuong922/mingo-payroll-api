using FluentValidation;
using MPS.Application.Features.Compliance.LegalDocument.Commands;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Validators;

public sealed class LinkLegalDocumentCommandValidator : AbstractValidator<LinkLegalDocumentCommand>
{
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ISocialInsuranceRateRepository _socialInsuranceRepo;
    private readonly IPitRegulationRepository _pitRepo;
    private readonly IMinimumWageRepository _minWageRepo;
    private readonly IAllowanceExemptThresholdRepository _allowanceRepo;
    private readonly ILaborLawConstantsRepository _laborLawRepo;

    public LinkLegalDocumentCommandValidator(
        ILegalDocumentRepository legalDocumentRepo,
        ISocialInsuranceRateRepository socialInsuranceRepo,
        IPitRegulationRepository pitRepo,
        IMinimumWageRepository minWageRepo,
        IAllowanceExemptThresholdRepository allowanceRepo,
        ILaborLawConstantsRepository laborLawRepo)
    {
        _legalDocumentRepo = legalDocumentRepo;
        _socialInsuranceRepo = socialInsuranceRepo;
        _pitRepo = pitRepo;
        _minWageRepo = minWageRepo;
        _allowanceRepo = allowanceRepo;
        _laborLawRepo = laborLawRepo;

        RuleFor(x => x.ComplianceEntityType).Must(t => ComplianceEntityTypes.All.Contains(t))
            .WithMessage($"ComplianceEntityType must be one of: {string.Join(", ", ComplianceEntityTypes.All)}.");

        RuleFor(x => x.LegalDocumentId).MustAsync(async (id, ct) =>
            await _legalDocumentRepo.GetByIdAsync(id, ct) is not null)
            .WithMessage(x => $"LegalDocument {x.LegalDocumentId} not found.");

        RuleFor(x => x).MustAsync(async (cmd, ct) =>
        {
            if (!ComplianceEntityTypes.All.Contains(cmd.ComplianceEntityType)) return true;
            return await VersionExistsAsync(cmd.ComplianceEntityType, cmd.ComplianceVersionId, ct);
        }).WithMessage(x => $"{x.ComplianceEntityType} version {x.ComplianceVersionId} not found.");
    }

    private async Task<bool> VersionExistsAsync(string entityType, int versionId, CancellationToken ct) =>
        entityType switch
        {
            ComplianceEntityTypes.SocialInsuranceRate => await _socialInsuranceRepo.GetByIdAsync(versionId, ct) is not null,
            ComplianceEntityTypes.PitRegulation => await _pitRepo.GetByIdAsync(versionId, ct) is not null,
            ComplianceEntityTypes.MinimumWage => await _minWageRepo.GetByIdAsync(versionId, ct) is not null,
            ComplianceEntityTypes.AllowanceExemptThreshold => await _allowanceRepo.GetByIdAsync(versionId, ct) is not null,
            ComplianceEntityTypes.LaborLawConstants => await _laborLawRepo.GetByIdAsync(versionId, ct) is not null,
            _ => false,
        };
}
