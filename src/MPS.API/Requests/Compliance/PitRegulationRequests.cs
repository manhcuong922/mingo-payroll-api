namespace MPS.API.Requests.Compliance;

public sealed record PitBracketInputRequest(int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record CreatePitRegulationRequest(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    IReadOnlyList<PitBracketInputRequest> Brackets,
    string LegalReference,
    string? Note);

public sealed record UpdatePitRegulationRequest(
    DateOnly ValidFrom,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    IReadOnlyList<PitBracketInputRequest> Brackets,
    string LegalReference,
    string? Note);
