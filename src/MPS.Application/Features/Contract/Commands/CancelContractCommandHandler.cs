using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public class CancelContractCommandHandler : IRequestHandler<CancelContractCommand, bool>
{
    private readonly IContractRepository _repository;

    public CancelContractCommandHandler(IContractRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(CancelContractCommand request, CancellationToken cancellationToken) =>
        _repository.CancelDraftAsync(request.Id, cancellationToken);
}
