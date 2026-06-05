using MediatR;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public class RemoveContractAllowanceCommandHandler : IRequestHandler<RemoveContractAllowanceCommand, bool>
{
    private readonly IContractRepository _repository;

    public RemoveContractAllowanceCommandHandler(IContractRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(RemoveContractAllowanceCommand request, CancellationToken cancellationToken)
    {
        var contract = await _repository.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken);
        if (contract is null) return false;

        if (contract.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot modify allowances on contract {request.ContractId}: status is {contract.Status}, only Draft can be modified.");

        return await _repository.RemoveAllowanceAsync(request.ContractId, request.AllowanceTypeId, cancellationToken);
    }
}
