using MediatR;

namespace MPS.Application.Features.AllowanceType.Commands;

public sealed record AllowanceTypeCreateCommand(
    string Code,
    string Name,
    string? Description,
    decimal? DefaultAmount,
    bool IsBhxhBase,
    bool IsTaxable,
    bool IsActive) : IRequest<int>;
