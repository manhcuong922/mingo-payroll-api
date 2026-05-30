using MPS.Domain.Entities;


namespace MPS.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> EmployeeCodeExistsAsync(string employeeCode, CancellationToken cancellationToken = default);
        Task<bool> UsernameExistsAsync(string username, string? excludeEmployeeCode = null, CancellationToken cancellationToken = default);
        Task<bool> EmailExistsAsync(string email, string? excludeEmployeeCode = null, CancellationToken cancellationToken = default);
        Task<string> AddAsync(User entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(User entity, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string employeeCode, CancellationToken cancellationToken = default);

    }
}
