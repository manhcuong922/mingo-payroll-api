using MediatR;

namespace MPS.Application.Features.Compliance.MinimumWage.Commands;

public sealed record MinimumWageRegionInput(string RegionCode, decimal AmountPerMonth);

public sealed record CreateMinimumWageCommand(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    IReadOnlyList<MinimumWageRegionInput> Regions,
    string LegalReference,
    string? Note) : IRequest<int>;

public sealed record UpdateMinimumWageCommand(
    int Id,
    DateOnly ValidFrom,
    IReadOnlyList<MinimumWageRegionInput> Regions,
    string LegalReference,
    string? Note) : IRequest<bool>;

public sealed record DeleteMinimumWagePendingCommand(int Id) : IRequest<bool>;
