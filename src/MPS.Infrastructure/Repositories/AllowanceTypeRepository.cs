using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class AllowanceTypeRepository : IAllowanceTypeRepository
{
    private readonly AppDbContext _context;

    public AllowanceTypeRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<AllowanceType?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.AllowanceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AllowanceType>> GetAllAsync(bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _context.AllowanceTypes.AsNoTracking();
        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        return await query
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim();
        var query = _context.AllowanceTypes.AsNoTracking()
            .Where(a => a.Code == normalized);
        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<int> AddAsync(AllowanceType entity, CancellationToken cancellationToken = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        await _context.AllowanceTypes.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(AllowanceType entity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.AllowanceTypes
            .FirstOrDefaultAsync(a => a.Id == entity.Id, cancellationToken);
        if (existing is null) return false;

        existing.Code = entity.Code;
        existing.Name = entity.Name;
        existing.Description = entity.Description;
        existing.DefaultAmount = entity.DefaultAmount;
        existing.IsBhxhBase = entity.IsBhxhBase;
        existing.IsTaxable = entity.IsTaxable;
        existing.IsActive = entity.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteOrDeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.AllowanceTypes
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (existing is null) return false;

        var referenced = await _context.ContractAllowances
            .AnyAsync(ca => ca.AllowanceTypeId == id, cancellationToken);
        if (referenced)
        {
            existing.IsActive = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.AllowanceTypes.Remove(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
