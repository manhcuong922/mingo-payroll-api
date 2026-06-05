using MediatR;

namespace MPS.Application.Features.Contract.Commands;

public sealed record AddContractAllowanceCommand(
    int ContractId,
    int AllowanceTypeId,
    decimal Amount,
    string? Currency,
    string? Note) : IRequest<bool>;
