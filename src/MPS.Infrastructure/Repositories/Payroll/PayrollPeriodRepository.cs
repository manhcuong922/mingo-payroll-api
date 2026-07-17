using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Payroll;

public sealed class PayrollPeriodRepository : IPayrollPeriodRepository
{
    private readonly AppDbContext _context;
    public PayrollPeriodRepository(AppDbContext context) => _context = context;

    public Task<PayrollPeriod?> GetByIdAsync(int id, bool asNoTracking, CancellationToken ct = default)
    {
        var q = _context.PayrollPeriods.AsQueryable();
        if (asNoTracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public Task<PayrollPeriod?> GetFullAsync(int id, bool asNoTracking, CancellationToken ct = default)
    {
        var q = _context.PayrollPeriods
            .Include(p => p.EmployerParty)
            .Include(p => p.ComplianceRefs)
            .Include(p => p.Notes)
            .Include(p => p.Rows).ThenInclude(r => r.Cells)
            .Include(p => p.Rows).ThenInclude(r => r.EmployeeInformation)
            .AsSplitQuery();
        if (asNoTracking) q = q.AsNoTrackingWithIdentityResolution();
        return q.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<PayrollPeriod>> GetAllAsync(int? year, int? month, CancellationToken ct = default)
    {
        var q = _context.PayrollPeriods.AsNoTracking();
        if (year.HasValue) q = q.Where(p => p.PeriodYear == year.Value);
        if (month.HasValue) q = q.Where(p => p.PeriodMonth == month.Value);
        return await q.OrderByDescending(p => p.PeriodYear).ThenByDescending(p => p.PeriodMonth).ToListAsync(ct);
    }

    public Task<bool> ExistsForPeriodAsync(int employerPartyId, int year, int month, CancellationToken ct = default) =>
        _context.PayrollPeriods.AsNoTracking()
            .AnyAsync(p => p.EmployerPartyId == employerPartyId && p.PeriodYear == year && p.PeriodMonth == month, ct);

    public Task<PayrollPeriod?> GetPreviousAsync(int employerPartyId, int year, int month, CancellationToken ct = default)
    {
        var current = year * 12 + month;
        return _context.PayrollPeriods.AsNoTracking()
            .Include(p => p.ComplianceRefs)
            .Where(p => p.EmployerPartyId == employerPartyId && (p.PeriodYear * 12 + p.PeriodMonth) < current)
            .OrderByDescending(p => p.PeriodYear * 12 + p.PeriodMonth)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> AddAsync(PayrollPeriod period, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await _context.PayrollPeriods.AddAsync(period, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return period.Id;
        });
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing is null) return false;
        _context.PayrollPeriods.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
