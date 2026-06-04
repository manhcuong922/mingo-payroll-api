using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractParty.Commands;

public class ContractPartyCreateCommandHandler : IRequestHandler<ContractPartyCreateCommand, int>
{
    private readonly IContractPartyRepository _repository;

    public ContractPartyCreateCommandHandler(IContractPartyRepository repository)
    {
        _repository = repository;
    }

    public Task<int> Handle(ContractPartyCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.ContractParty
        {
            LegalName = request.LegalName.Trim(),
            TaxCode = request.TaxCode.Trim(),
            Address = request.Address.Trim(),
            RepresentativeFullName = request.RepresentativeFullName.Trim(),
            RepresentativeTitle = request.RepresentativeTitle.Trim(),
        };
        return _repository.CreateNewVersionAsync(entity, cancellationToken);
    }
}
