using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Payroll;

public sealed class PieceWorkSupportBandRepository : IPieceWorkSupportBandRepository
{
    private readonly AppDbContext _context;
    public PieceWorkSupportBandRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<PieceWorkSupportBand>> GetAllAsync(bool? isActive, CancellationToken ct = default)
    {
        var q = _context.PieceWorkSupportBands.AsNoTracking();
        if (isActive.HasValue) q = q.Where(b => b.IsActive == isActive.Value);
        return await q.OrderBy(b => b.MinHours).ToListAsync(ct);
    }

    public Task<PieceWorkSupportBand?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _context.PieceWorkSupportBands.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<int> AddAsync(PieceWorkSupportBand entity, CancellationToken ct = default)
    {
        await _context.PieceWorkSupportBands.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(PieceWorkSupportBand entity, CancellationToken ct = default)
    {
        var existing = await _context.PieceWorkSupportBands.FirstOrDefaultAsync(b => b.Id == entity.Id, ct);
        if (existing is null) return false;
        existing.MinHours = entity.MinHours;
        existing.MaxHours = entity.MaxHours;
        existing.Amount = entity.Amount;
        existing.Label = entity.Label;
        existing.IsActive = entity.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _context.PieceWorkSupportBands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (existing is null) return false;
        _context.PieceWorkSupportBands.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
