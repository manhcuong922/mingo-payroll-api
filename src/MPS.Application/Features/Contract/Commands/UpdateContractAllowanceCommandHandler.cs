using MediatR;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public class UpdateContractAllowanceCommandHandler : IRequestHandler<UpdateContractAllowanceCommand, bool>
{
    private readonly IContractRepository _repository;

    public UpdateContractAllowanceCommandHandler(IContractRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdateContractAllowanceCommand request, CancellationToken cancellationToken)
    {
        var contract = await _repository.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken);
        if (contract is null) return false;

        if (contract.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot modify allowances on contract {request.ContractId}: status is {contract.Status}, only Draft can be modified.");

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? contract.SalaryCurrencySnapshot
            : request.Currency.Trim().ToUpperInvariant();

        return await _repository.UpdateAllowanceAmountAsync(
            request.ContractId,
            request.AllowanceTypeId,
            request.Amount,
            currency,
            request.Note?.Trim(),
            cancellationToken);
    }
}
