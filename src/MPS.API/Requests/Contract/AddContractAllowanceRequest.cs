namespace MPS.API.Requests.Contract;

public sealed record AddContractAllowanceRequest(
    int AllowanceTypeId,
    decimal Amount,
    string? Currency,
    string? Note);
