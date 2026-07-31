namespace MPS.Application.DTOs;

public sealed record AllowanceTypeDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    decimal? DefaultAmount,
    bool IsBhxhBase,
    bool IsTaxable,
    bool ExcludeFromProration,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
