using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTemplate.Commands;

public class ContractTemplateDeleteCommandHandler : IRequestHandler<ContractTemplateDeleteCommand, bool>
{
    private readonly IContractTemplateRepository _repository;

    public ContractTemplateDeleteCommandHandler(IContractTemplateRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(ContractTemplateDeleteCommand request, CancellationToken cancellationToken) =>
        _repository.DeleteOrDeactivateAsync(request.Id, cancellationToken);
}
