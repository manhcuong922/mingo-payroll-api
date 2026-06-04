using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class SalaryGradeRepository : ISalaryGradeRepository
{
    private readonly AppDbContext _context;

    public SalaryGradeRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<SalaryGrade?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.SalaryGrades
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SalaryGrade>> GetAllAsync(int? positionId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _context.SalaryGrades.AsNoTracking();
        if (positionId.HasValue)
            query = query.Where(s => s.PositionId == positionId.Value);
        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        return await query
            .OrderBy(s => s.PositionId)
            .ThenBy(s => s.Level)
            .ThenBy(s => s.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim();
        var query = _context.SalaryGrades.AsNoTracking()
            .Where(s => s.Code == normalized);
        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasContractsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Contracts.AsNoTracking().AnyAsync(c => c.SalaryGradeId == id, cancellationToken);

    public async Task<int> AddAsync(SalaryGrade entity, CancellationToken cancellationToken = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        await _context.SalaryGrades.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(SalaryGrade entity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.SalaryGrades
            .FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        if (existing is null) return false;

        existing.Code = entity.Code;
        existing.Name = entity.Name;
        existing.PositionId = entity.PositionId;
        existing.Level = entity.Level;
        existing.BaseSalary = entity.BaseSalary;
        existing.Currency = entity.Currency;
        existing.Description = entity.Description;
        existing.IsActive = entity.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteOrDeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.SalaryGrades
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (existing is null) return false;

        var referenced = await _context.Contracts.AnyAsync(c => c.SalaryGradeId == id, cancellationToken);
        if (referenced)
        {
            existing.IsActive = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.SalaryGrades.Remove(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
