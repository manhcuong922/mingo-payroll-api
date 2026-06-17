namespace MPS.API.Requests.Compliance;

public sealed record CreateLegalDocumentRequest(
    string Code,
    string DocumentType,
    string DocumentNumber,
    string Title,
    string IssuedBy,
    DateOnly IssuedDate,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate,
    string? Url,
    string? Note);
