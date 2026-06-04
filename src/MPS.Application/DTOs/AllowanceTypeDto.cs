namespace MPS.Application.DTOs;

public sealed record AllowanceTypeDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    decimal? DefaultAmount,
    bool IsBhxhBase,
    bool IsTaxable,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
