namespace MPS.API.Requests.Compliance;

public sealed record UnlinkLegalDocumentRequest(
    string ComplianceEntityType,
    int ComplianceVersionId,
    int LegalDocumentId);
