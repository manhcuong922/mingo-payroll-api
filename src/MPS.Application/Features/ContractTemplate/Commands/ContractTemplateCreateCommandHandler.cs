using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTemplate.Commands;

public class ContractTemplateCreateCommandHandler : IRequestHandler<ContractTemplateCreateCommand, int>
{
    private readonly IContractTemplateRepository _repository;

    public ContractTemplateCreateCommandHandler(IContractTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(ContractTemplateCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.ContractTemplate
        {
            Name = request.Name.Trim(),
            ContractCategory = request.ContractCategory,
            ContractSubtype = request.ContractSubtype,
            Version = 1,
            IsActive = request.IsActive,
        };
        return await _repository.AddAsync(entity, cancellationToken);
    }
}
