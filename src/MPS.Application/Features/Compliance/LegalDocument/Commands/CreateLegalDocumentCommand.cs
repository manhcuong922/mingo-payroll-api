using MediatR;

namespace MPS.Application.Features.Compliance.LegalDocument.Commands;

public sealed record CreateLegalDocumentCommand(
    string Code,
    string DocumentType,
    string DocumentNumber,
    string Title,
    string IssuedBy,
    DateOnly IssuedDate,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate,
    string? Url,
    string? Note) : IRequest<int>;
