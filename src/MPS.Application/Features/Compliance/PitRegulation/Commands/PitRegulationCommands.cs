using MediatR;
using MPS.Application.DTOs.Compliance;

namespace MPS.Application.Features.Compliance.PitRegulation.Commands;

public sealed record PitBracketInput(int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record CreatePitRegulationCommand(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    IReadOnlyList<PitBracketInput> Brackets,
    string LegalReference,
    string? Note) : IRequest<int>;

public sealed record UpdatePitRegulationCommand(
    int Id,
    DateOnly ValidFrom,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    IReadOnlyList<PitBracketInput> Brackets,
    string LegalReference,
    string? Note) : IRequest<bool>;

public sealed record DeletePitRegulationPendingCommand(int Id) : IRequest<bool>;
