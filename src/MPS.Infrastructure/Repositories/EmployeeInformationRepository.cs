using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories
{
    public class EmployeeInformationRepository : IEmployeeInformationRepository
    {
        private readonly AppDbContext _context;

        public EmployeeInformationRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<EmployeeInformation?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            _context.EmployeeInformations
                .AsNoTracking()
                .Include(e => e.Position)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        public Task<EmployeeInformation?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default) =>
            _context.EmployeeInformations
                .AsNoTracking()
                .Include(e => e.Position)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.EmployeeCode == employeeCode, cancellationToken);

        public async Task<IReadOnlyList<EmployeeInformation>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await _context.EmployeeInformations
                .AsNoTracking()
                .Include(e => e.Position)
                .Include(e => e.User)
                .ToListAsync(cancellationToken);

        public Task<bool> UserExistsAsync(string employeeCode, CancellationToken cancellationToken = default) =>
            _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.EmployeeCode == employeeCode, cancellationToken);

        public Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken = default) =>
            _context.Positions
                .AsNoTracking()
                .AnyAsync(p => p.Id == positionId, cancellationToken);

        public async Task<int> AddAsync(EmployeeInformation entity, CancellationToken cancellationToken = default)
        {
            var existed = await _context.EmployeeInformations
                .AsNoTracking()
                .AnyAsync(e => e.EmployeeCode == entity.EmployeeCode, cancellationToken);

            if (existed)
                throw new DomainValidationException($"EmployeeInformation already exists for EmployeeCode '{entity.EmployeeCode}'.");

            await _context.EmployeeInformations.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }

        public async Task<bool> UpdateAsync(EmployeeInformation entity, CancellationToken cancellationToken = default)
        {
            var existing = await _context.EmployeeInformations
                .FirstOrDefaultAsync(e => e.Id == entity.Id, cancellationToken);

            if (existing is null) return false;

            existing.EmployeeCode = entity.EmployeeCode;
            existing.TaxNo = entity.TaxNo;
            existing.SocialNo = entity.SocialNo;
            existing.BankNumber = entity.BankNumber;
            existing.PositionId = entity.PositionId;
            existing.DependentsNo = entity.DependentsNo;
            existing.CitizenId = entity.CitizenId;
            existing.Address = entity.Address;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var existing = await _context.EmployeeInformations
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

            if (existing is null) return false;

            _context.EmployeeInformations.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
