using MediatR;

namespace MPS.Application.Features.Compliance.MinimumWage.Commands;

public sealed record MinimumWageRegionInput(string RegionCode, decimal AmountPerMonth);

public sealed record CreateMinimumWageCommand(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    string AppliedRegionCode,
    IReadOnlyList<MinimumWageRegionInput> Regions,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds) : IRequest<int>;

public sealed record UpdateMinimumWageCommand(
    int Id,
    DateOnly ValidFrom,
    string AppliedRegionCode,
    IReadOnlyList<MinimumWageRegionInput> Regions,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds) : IRequest<bool>;

public sealed record DeleteMinimumWagePendingCommand(int Id) : IRequest<bool>;
