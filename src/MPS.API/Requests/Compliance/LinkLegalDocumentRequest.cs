namespace MPS.API.Requests.Compliance;

public sealed record LinkLegalDocumentRequest(
    string ComplianceEntityType,
    int ComplianceVersionId,
    int LegalDocumentId);
