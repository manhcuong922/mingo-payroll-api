using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IAllowanceTypeRepository
{
    Task<AllowanceType?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AllowanceType>> GetAllAsync(bool? isActive, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> AddAsync(AllowanceType entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(AllowanceType entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard delete nếu không có ContractAllowance tham chiếu; ngược lại set IsActive=false (soft).
    /// </summary>
    Task<bool> DeleteOrDeactivateAsync(int id, CancellationToken cancellationToken = default);
}
