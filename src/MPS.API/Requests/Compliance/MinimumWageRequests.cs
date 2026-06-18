namespace MPS.API.Requests.Compliance;

public sealed record MinimumWageRegionInputRequest(string RegionCode, decimal AmountPerMonth);

public sealed record CreateMinimumWageRequest(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    string AppliedRegionCode,
    IReadOnlyList<MinimumWageRegionInputRequest> Regions,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);

public sealed record UpdateMinimumWageRequest(
    DateOnly ValidFrom,
    string AppliedRegionCode,
    IReadOnlyList<MinimumWageRegionInputRequest> Regions,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);
