using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IContractPartyRepository
{
    Task<ContractParty?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractParty>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Append-only versioning: trong cùng 1 transaction set EffectiveTo cho row active
    /// hiện tại rồi insert row mới. Trả về Id của row mới.
    /// </summary>
    Task<int> CreateNewVersionAsync(ContractParty newVersion, CancellationToken cancellationToken = default);
}
