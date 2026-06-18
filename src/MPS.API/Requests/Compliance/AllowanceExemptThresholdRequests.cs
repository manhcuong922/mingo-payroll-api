namespace MPS.API.Requests.Compliance;

public sealed record CreateAllowanceExemptThresholdRequest(
    int AllowanceTypeId,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);

public sealed record UpdateAllowanceExemptThresholdRequest(
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);
