using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories
{
    public class PositionRepository : IPositionRepository
    {
        private readonly AppDbContext _context;

        public PositionRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Position?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            _context.Positions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        public async Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await _context.Positions
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);

        public Task<bool> NameExistsAsync(
            string name,
            int? excludeId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Positions
                .AsNoTracking()
                .Where(p => p.Name == name);

            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);

            return query.AnyAsync(cancellationToken);
        }

        public Task<bool> CodeExistsAsync(
            string code,
            int? excludeId = null,
            CancellationToken cancellationToken = default)
        {
            var normalized = code.Trim().ToUpperInvariant();
            var query = _context.Positions
                .AsNoTracking()
                .Where(p => p.Code == normalized);

            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);

            return query.AnyAsync(cancellationToken);
        }

        public Task<bool> HasEmployeeInformationsAsync(int id, CancellationToken cancellationToken = default) =>
            _context.EmployeeInformations
                .AsNoTracking()
                .AnyAsync(e => e.PositionId == id, cancellationToken);

        public async Task<int> AddAsync(Position entity, CancellationToken cancellationToken = default)
        {
            await _context.Positions.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }

        public async Task<bool> UpdateAsync(Position entity, CancellationToken cancellationToken = default)
        {
            var existing = await _context.Positions
                .FirstOrDefaultAsync(p => p.Id == entity.Id, cancellationToken);

            if (existing is null) return false;

            existing.Code = entity.Code;
            existing.Name = entity.Name;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var existing = await _context.Positions
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (existing is null) return false;

            _context.Positions.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
