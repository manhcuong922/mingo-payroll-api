using MediatR;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public class AddContractAllowanceCommandHandler : IRequestHandler<AddContractAllowanceCommand, bool>
{
    private readonly IContractRepository _repository;
    private readonly IContractLookupReader _lookup;

    public AddContractAllowanceCommandHandler(IContractRepository repository, IContractLookupReader lookup)
    {
        _repository = repository;
        _lookup = lookup;
    }

    public async Task<bool> Handle(AddContractAllowanceCommand request, CancellationToken cancellationToken)
    {
        var contract = await _repository.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken);
        if (contract is null) return false;

        if (contract.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot modify allowances on contract {request.ContractId}: status is {contract.Status}, only Draft can be modified.");

        if (!await _lookup.IsActiveAllowanceTypeAsync(request.AllowanceTypeId, cancellationToken))
            throw new DomainValidationException($"AllowanceType {request.AllowanceTypeId} not found or inactive.");

        var entity = new ContractAllowance
        {
            ContractId = request.ContractId,
            AllowanceTypeId = request.AllowanceTypeId,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency)
                ? contract.SalaryCurrencySnapshot
                : request.Currency.Trim().ToUpperInvariant(),
            Note = request.Note?.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        return await _repository.AddAllowanceAsync(request.ContractId, entity, cancellationToken);
    }
}
