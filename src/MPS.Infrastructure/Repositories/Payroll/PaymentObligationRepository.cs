using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Payroll.BhxhTax;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Payroll;

public sealed class PaymentObligationRepository : IPaymentObligationRepository
{
    private readonly AppDbContext _context;

    public PaymentObligationRepository(AppDbContext context) => _context = context;

    public Task<PaymentObligation?> GetByIdAsync(int id, bool asNoTracking, CancellationToken ct = default)
    {
        var q = _context.Set<PaymentObligation>().Include(o => o.PayrollPeriod).AsQueryable();
        if (asNoTracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<IReadOnlyList<PaymentObligation>> GetByPeriodAsync(int periodId, CancellationToken ct = default)
    {
        return await _context.Set<PaymentObligation>().AsNoTracking()
            .Where(o => o.PayrollPeriodId == periodId)
            .OrderBy(o => o.ObligationType)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentObligation>> QueryAsync(
        int? year, int? month, PaymentObligationType? type, PaymentObligationStatus? status,
        CancellationToken ct = default)
    {
        var q = _context.Set<PaymentObligation>().AsNoTracking()
            .Include(o => o.PayrollPeriod)
            .AsQueryable();

        if (year.HasValue) q = q.Where(o => o.PayrollPeriod.PeriodYear == year.Value);
        if (month.HasValue) q = q.Where(o => o.PayrollPeriod.PeriodMonth == month.Value);
        if (type.HasValue) q = q.Where(o => o.ObligationType == type.Value);
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);

        return await q
            .OrderByDescending(o => o.PayrollPeriod.PeriodYear)
            .ThenByDescending(o => o.PayrollPeriod.PeriodMonth)
            .ThenBy(o => o.ObligationType)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentObligation>> ListPendingWithDeadlineBeforeAsync(
        DateTime deadlineExclusiveUpperBound, CancellationToken ct = default)
    {
        return await _context.Set<PaymentObligation>()
            .Where(o => o.Status == PaymentObligationStatus.Pending
                        && o.Deadline < deadlineExclusiveUpperBound)
            .ToListAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<PaymentObligation> obligations, CancellationToken ct = default)
    {
        await _context.Set<PaymentObligation>().AddRangeAsync(obligations, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
