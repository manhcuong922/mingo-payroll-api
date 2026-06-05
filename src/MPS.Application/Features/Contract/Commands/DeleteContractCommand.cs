using MediatR;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public sealed record DeleteContractCommand(int Id) : IRequest<bool>, IContractScopedCommand
{
    int IContractScopedCommand.ContractId => Id;
}
