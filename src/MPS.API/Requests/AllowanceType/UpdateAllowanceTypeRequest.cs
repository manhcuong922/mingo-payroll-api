namespace MPS.API.Requests.AllowanceType;

public sealed record UpdateAllowanceTypeRequest(
    string Code,
    string Name,
    string? Description,
    decimal? DefaultAmount,
    bool IsBhxhBase,
    bool IsTaxable,
    bool IsActive);
