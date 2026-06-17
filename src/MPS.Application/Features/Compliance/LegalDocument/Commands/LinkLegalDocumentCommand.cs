using MediatR;

namespace MPS.Application.Features.Compliance.LegalDocument.Commands;

public sealed record LinkLegalDocumentCommand(
    string ComplianceEntityType,
    int ComplianceVersionId,
    int LegalDocumentId) : IRequest<int>;
