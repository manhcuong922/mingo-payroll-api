using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> EmployeeCodeExistsAsync(string employeeCode, int? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> RoleExistsAsync(int roleId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(User entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(User entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateLastLoginAsync(int userId, DateTime loginAt, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
