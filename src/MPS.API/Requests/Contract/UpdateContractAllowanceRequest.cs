namespace MPS.API.Requests.Contract;

public sealed record UpdateContractAllowanceRequest(
    decimal Amount,
    string? Currency,
    string? Note);
