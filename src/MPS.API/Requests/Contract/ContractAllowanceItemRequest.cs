namespace MPS.API.Requests.Contract;

public sealed record ContractAllowanceItemRequest(
    int AllowanceTypeId,
    decimal Amount,
    string? Currency,
    string? Note);
