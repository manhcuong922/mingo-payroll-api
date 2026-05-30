using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Role?> GetByIdWithPermissionsAsync(int id, CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, int? excludeRoleId = null, CancellationToken cancellationToken = default);
    Task<bool> HasUsersAsync(int roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetUserIdsByRoleAsync(int roleId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(Role entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Role entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
