using MediatR;

namespace MPS.Application.Features.Contract.Commands;

public sealed record DeleteContractCommand(int Id) : IRequest<bool>;
