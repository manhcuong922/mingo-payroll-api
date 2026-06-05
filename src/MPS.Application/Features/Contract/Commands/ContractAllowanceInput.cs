namespace MPS.Application.Features.Contract.Commands;

public sealed record ContractAllowanceInput(
    int AllowanceTypeId,
    decimal Amount,
    string? Currency,
    string? Note);
