namespace MPS.API.Requests.Compliance;

public sealed record OtRateEntryInputRequest(string OtType, string Label, decimal Rate);

public sealed record CreateOtRateRequest(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    IReadOnlyList<OtRateEntryInputRequest> Entries,
    string LegalReference,
    string? Note);

public sealed record UpdateOtRateRequest(
    DateOnly ValidFrom,
    IReadOnlyList<OtRateEntryInputRequest> Entries,
    string LegalReference,
    string? Note);
