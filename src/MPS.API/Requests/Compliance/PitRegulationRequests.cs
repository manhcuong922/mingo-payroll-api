namespace MPS.API.Requests.Compliance;

public sealed record PitBracketInputRequest(int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record CreatePitRegulationRequest(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    int ShortTermContractMaxMonths,
    decimal ShortTermFlatRatePercent,
    decimal ShortTermTaxExemptMonthlyIncome,
    IReadOnlyList<PitBracketInputRequest> Brackets,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);

public sealed record UpdatePitRegulationRequest(
    DateOnly ValidFrom,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    int ShortTermContractMaxMonths,
    decimal ShortTermFlatRatePercent,
    decimal ShortTermTaxExemptMonthlyIncome,
    IReadOnlyList<PitBracketInputRequest> Brackets,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);
