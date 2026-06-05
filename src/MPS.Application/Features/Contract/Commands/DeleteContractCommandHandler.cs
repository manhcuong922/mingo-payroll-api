using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public class DeleteContractCommandHandler : IRequestHandler<DeleteContractCommand, bool>
{
    private readonly IContractRepository _repository;

    public DeleteContractCommandHandler(IContractRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(DeleteContractCommand request, CancellationToken cancellationToken) =>
        _repository.DeleteDraftAsync(request.Id, cancellationToken);
}
