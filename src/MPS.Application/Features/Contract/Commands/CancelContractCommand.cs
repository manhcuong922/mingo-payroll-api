using MediatR;

namespace MPS.Application.Features.Contract.Commands;

public sealed record CancelContractCommand(int Id) : IRequest<bool>;
