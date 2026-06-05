namespace MPS.Application.DTOs;

public sealed record ContractAllowanceItemDto(
    int AllowanceTypeId,
    string Code,
    string Name,
    decimal Amount,
    string Currency,
    string? Note);
