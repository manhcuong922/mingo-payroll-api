using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Payroll;

public sealed class ProductivityCoefficientRepository : IProductivityCoefficientRepository
{
    private readonly AppDbContext _context;
    public ProductivityCoefficientRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<ProductivityCoefficient>> GetAllAsync(bool? isActive, CancellationToken ct = default)
    {
        var q = _context.ProductivityCoefficients.AsNoTracking();
        if (isActive.HasValue) q = q.Where(p => p.IsActive == isActive.Value);
        return await q.OrderBy(p => p.MinScore).ToListAsync(ct);
    }

    public Task<ProductivityCoefficient?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _context.ProductivityCoefficients.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<int> AddAsync(ProductivityCoefficient entity, CancellationToken ct = default)
    {
        await _context.ProductivityCoefficients.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(ProductivityCoefficient entity, CancellationToken ct = default)
    {
        var existing = await _context.ProductivityCoefficients.FirstOrDefaultAsync(p => p.Id == entity.Id, ct);
        if (existing is null) return false;
        existing.MinScore = entity.MinScore;
        existing.MaxScore = entity.MaxScore;
        existing.Coefficient = entity.Coefficient;
        existing.Label = entity.Label;
        existing.IsActive = entity.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _context.ProductivityCoefficients.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing is null) return false;
        _context.ProductivityCoefficients.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
