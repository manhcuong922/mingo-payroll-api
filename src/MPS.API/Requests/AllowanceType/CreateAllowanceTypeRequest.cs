namespace MPS.API.Requests.AllowanceType;

public sealed record CreateAllowanceTypeRequest(
    string Code,
    string Name,
    string? Description,
    decimal? DefaultAmount,
    bool IsBhxhBase = false,
    bool IsTaxable = true,
    bool IsActive = true);
