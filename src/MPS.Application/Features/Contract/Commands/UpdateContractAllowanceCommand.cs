using MediatR;

namespace MPS.Application.Features.Contract.Commands;

public sealed record UpdateContractAllowanceCommand(
    int ContractId,
    int AllowanceTypeId,
    decimal Amount,
    string? Currency,
    string? Note) : IRequest<bool>;
