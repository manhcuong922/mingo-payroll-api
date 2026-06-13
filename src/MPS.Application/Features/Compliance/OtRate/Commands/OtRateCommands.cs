using MediatR;

namespace MPS.Application.Features.Compliance.OtRate.Commands;

public sealed record OtRateEntryInput(string OtType, string Label, decimal Rate);

public sealed record CreateOtRateCommand(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    IReadOnlyList<OtRateEntryInput> Entries,
    string LegalReference,
    string? Note) : IRequest<int>;

public sealed record UpdateOtRateCommand(
    int Id,
    DateOnly ValidFrom,
    IReadOnlyList<OtRateEntryInput> Entries,
    string LegalReference,
    string? Note) : IRequest<bool>;

public sealed record DeleteOtRatePendingCommand(int Id) : IRequest<bool>;
