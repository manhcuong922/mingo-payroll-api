using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Permission?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Permission>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
}
