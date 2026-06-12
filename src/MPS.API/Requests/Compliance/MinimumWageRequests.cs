namespace MPS.API.Requests.Compliance;

public sealed record MinimumWageRegionInputRequest(string RegionCode, decimal AmountPerMonth);

public sealed record CreateMinimumWageRequest(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    IReadOnlyList<MinimumWageRegionInputRequest> Regions,
    string LegalReference,
    string? Note);

public sealed record UpdateMinimumWageRequest(
    DateOnly ValidFrom,
    IReadOnlyList<MinimumWageRegionInputRequest> Regions,
    string LegalReference,
    string? Note);
