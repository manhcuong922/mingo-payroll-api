using MediatR;

namespace MPS.Application.Features.Contract.Commands;

public sealed record RemoveContractAllowanceCommand(int ContractId, int AllowanceTypeId) : IRequest<bool>;
