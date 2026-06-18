using MediatR;

namespace MPS.Application.Features.Compliance.LegalDocument.Commands;

public sealed record UnlinkLegalDocumentCommand(
    string ComplianceEntityType,
    int ComplianceVersionId,
    int LegalDocumentId) : IRequest<bool>;
