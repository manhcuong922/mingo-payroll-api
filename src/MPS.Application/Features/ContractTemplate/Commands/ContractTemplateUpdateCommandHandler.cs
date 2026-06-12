using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTemplate.Commands;

public class ContractTemplateUpdateCommandHandler : IRequestHandler<ContractTemplateUpdateCommand, bool>
{
    private readonly IContractTemplateRepository _repository;

    public ContractTemplateUpdateCommandHandler(IContractTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(ContractTemplateUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.ContractTemplate
        {
            Id = request.Id,
            Name = request.Name.Trim(),
            ContractCategory = request.ContractCategory,
            ContractSubtype = request.ContractSubtype,
            IsActive = request.IsActive,
        };
        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
