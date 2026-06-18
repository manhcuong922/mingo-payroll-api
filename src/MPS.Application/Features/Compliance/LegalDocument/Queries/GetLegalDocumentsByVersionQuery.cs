using MediatR;
using MPS.Application.DTOs.Compliance;

namespace MPS.Application.Features.Compliance.LegalDocument.Queries;

public sealed record GetLegalDocumentsByVersionQuery(
    string ComplianceEntityType,
    int ComplianceVersionId) : IRequest<IReadOnlyList<LegalDocumentSummaryDto>>;
