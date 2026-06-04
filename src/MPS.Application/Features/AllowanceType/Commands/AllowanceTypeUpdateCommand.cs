using MediatR;

namespace MPS.Application.Features.AllowanceType.Commands;

public sealed record AllowanceTypeUpdateCommand(
    int Id,
    string Code,
    string Name,
    string? Description,
    decimal? DefaultAmount,
    bool IsBhxhBase,
    bool IsTaxable,
    bool IsActive) : IRequest<bool>;
