using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;

namespace MPS.Domain.Interfaces;

public interface IContractTemplateRepository
{
    Task<ContractTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractTemplate>> GetAllAsync(
        ContractCategory? category,
        ContractSubtype? subtype,
        bool? isActive,
        CancellationToken cancellationToken = default);
    Task<int> AddAsync(ContractTemplate entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ContractTemplate entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard delete nếu không có Contract tham chiếu; ngược lại set IsActive=false (soft).
    /// </summary>
    Task<bool> DeleteOrDeactivateAsync(int id, CancellationToken cancellationToken = default);
}
