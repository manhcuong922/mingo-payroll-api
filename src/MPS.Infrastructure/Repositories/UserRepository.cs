using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<User?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default) =>
            _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.EmployeeInformation)
                .FirstOrDefaultAsync(u => u.EmployeeCode == employeeCode, cancellationToken);

        public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.EmployeeInformation)
                .ToListAsync(cancellationToken);

        public Task<bool> EmployeeCodeExistsAsync(string employeeCode, CancellationToken cancellationToken = default) =>
            _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.EmployeeCode == employeeCode, cancellationToken);

        public Task<bool> UsernameExistsAsync(
            string username,
            string? excludeEmployeeCode = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Users
                .AsNoTracking()
                .Where(u => u.Username == username);

            if (!string.IsNullOrWhiteSpace(excludeEmployeeCode))
                query = query.Where(u => u.EmployeeCode != excludeEmployeeCode);

            return query.AnyAsync(cancellationToken);
        }

        public Task<bool> EmailExistsAsync(
            string email,
            string? excludeEmployeeCode = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Users
                .AsNoTracking()
                .Where(u => u.Email == email);

            if (!string.IsNullOrWhiteSpace(excludeEmployeeCode))
                query = query.Where(u => u.EmployeeCode != excludeEmployeeCode);

            return query.AnyAsync(cancellationToken);
        }

        public async Task<string> AddAsync(User entity, CancellationToken cancellationToken = default)
        {
            await _context.Users.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return entity.EmployeeCode;
        }

        public async Task<bool> UpdateAsync(User entity, CancellationToken cancellationToken = default)
        {
            var existing = await _context.Users
                .FirstOrDefaultAsync(u => u.EmployeeCode == entity.EmployeeCode, cancellationToken);

            if (existing is null) return false;

            existing.FullName = entity.FullName;
            existing.RoleId = entity.RoleId;
            existing.Username = entity.Username;
            existing.Password = entity.Password;
            existing.Email = entity.Email;
            existing.Dob = entity.Dob;
            existing.PhoneNo = entity.PhoneNo;
            existing.Status = entity.Status;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(string employeeCode, CancellationToken cancellationToken = default)
        {
            var existing = await _context.Users
                .FirstOrDefaultAsync(u => u.EmployeeCode == employeeCode, cancellationToken);

            if (existing is null) return false;

            _context.Users.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
