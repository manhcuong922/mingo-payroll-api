using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Payroll;

public sealed class PayrollParameterRepository : IPayrollParameterRepository
{
    private readonly AppDbContext _context;
    public PayrollParameterRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<PayrollParameter>> GetAllAsync(bool? isActive, CancellationToken ct = default)
    {
        var q = _context.PayrollParameters.AsNoTracking();
        if (isActive.HasValue) q = q.Where(p => p.IsActive == isActive.Value);
        return await q.OrderBy(p => p.Key).ToListAsync(ct);
    }

    public Task<PayrollParameter?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _context.PayrollParameters.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<PayrollParameter?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        _context.PayrollParameters.AsNoTracking().FirstOrDefaultAsync(p => p.Key == key, ct);

    public async Task<int> AddAsync(PayrollParameter entity, CancellationToken ct = default)
    {
        await _context.PayrollParameters.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(PayrollParameter entity, CancellationToken ct = default)
    {
        var existing = await _context.PayrollParameters.FirstOrDefaultAsync(p => p.Id == entity.Id, ct);
        if (existing is null) return false;
        existing.Name = entity.Name;
        existing.Value = entity.Value;
        existing.Description = entity.Description;
        existing.IsActive = entity.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> KeyExistsAsync(string key, int? excludeId, CancellationToken ct = default) =>
        _context.PayrollParameters.AsNoTracking()
            .AnyAsync(p => p.Key == key && (!excludeId.HasValue || p.Id != excludeId.Value), ct);
}
