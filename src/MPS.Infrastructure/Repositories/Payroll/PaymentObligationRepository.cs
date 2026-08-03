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

    // ===== Obligation =====

    public Task<PaymentObligation?> GetByIdAsync(int id, bool asNoTracking, CancellationToken ct = default)
    {
        var q = _context.Set<PaymentObligation>().Include(o => o.PayrollPeriod).AsQueryable();
        if (asNoTracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<IReadOnlyList<PaymentObligation>> GetByPeriodAsync(int periodId, CancellationToken ct = default)
    {
        return await _context.Set<PaymentObligation>()
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

    public async Task<IReadOnlyList<PaymentObligation>> ListPendingOrPartiallyPaidWithDeadlineBeforeAsync(
        DateTime deadlineExclusiveUpperBound, CancellationToken ct = default)
    {
        return await _context.Set<PaymentObligation>()
            .Where(o => (o.Status == PaymentObligationStatus.Pending
                            || o.Status == PaymentObligationStatus.PartiallyPaid)
                        && o.Deadline < deadlineExclusiveUpperBound)
            .ToListAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<PaymentObligation> obligations, CancellationToken ct = default)
    {
        await _context.Set<PaymentObligation>().AddRangeAsync(obligations, ct);
    }

    // ===== Line =====

    public Task<PaymentObligationLine?> GetLineByIdAsync(int lineId, bool asNoTracking, CancellationToken ct = default)
    {
        // v3.1: Include EmployeeInformation + PayrollPeriod để handler dựng payload thông báo
        // (EmployeeUserId, PeriodYear/Month) mà không cần round-trip DB phụ.
        var q = _context.Set<PaymentObligationLine>()
            .Include(l => l.PaymentObligation)
                .ThenInclude(o => o.PayrollPeriod)
            .Include(l => l.EmployeeInformation)
            .AsQueryable();
        if (asNoTracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(l => l.Id == lineId, ct);
    }

    public async Task AddLinesAsync(IEnumerable<PaymentObligationLine> lines, CancellationToken ct = default)
    {
        await _context.Set<PaymentObligationLine>().AddRangeAsync(lines, ct);
    }

    public async Task DeleteLinesByObligationAsync(int obligationId, CancellationToken ct = default)
    {
        var lines = await _context.Set<PaymentObligationLine>()
            .Where(l => l.PaymentObligationId == obligationId)
            .ToListAsync(ct);
        if (lines.Count > 0)
            _context.Set<PaymentObligationLine>().RemoveRange(lines);
    }

    public async Task<(IReadOnlyList<PaymentObligationLine> Items, int TotalCount)> GetLinesByObligationAsync(
        int obligationId,
        PaymentLineStatus? statusFilter,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _context.Set<PaymentObligationLine>().AsNoTracking()
            .Where(l => l.PaymentObligationId == obligationId);

        if (statusFilter.HasValue)
            q = q.Where(l => l.LineStatus == statusFilter.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(l =>
                EF.Functions.Like(l.EmployeeCodeSnapshot, $"%{term}%") ||
                EF.Functions.Like(l.EmployeeFullNameSnapshot, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);

        // Sort: Pending trước (LineStatus asc: Pending=1, Paid=2, Excluded=3) → TotalAmount DESC.
        var items = await q
            .OrderBy(l => l.LineStatus)
            .ThenByDescending(l => l.TotalAmount)
            .ThenBy(l => l.EmployeeCodeSnapshot)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<PaymentObligationLine>> GetAllLinesByObligationAsync(
        int obligationId, CancellationToken ct = default)
    {
        return await _context.Set<PaymentObligationLine>().AsNoTracking()
            .Where(l => l.PaymentObligationId == obligationId)
            .OrderBy(l => l.EmployeeCodeSnapshot)
            .ToListAsync(ct);
    }

    public async Task<PaymentObligationLineCounts> CountLinesByStatusAsync(int obligationId, CancellationToken ct = default)
    {
        var groups = await _context.Set<PaymentObligationLine>().AsNoTracking()
            .Where(l => l.PaymentObligationId == obligationId)
            .GroupBy(l => l.LineStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pending = groups.FirstOrDefault(g => g.Status == PaymentLineStatus.Pending)?.Count ?? 0;
        var paid = groups.FirstOrDefault(g => g.Status == PaymentLineStatus.Paid)?.Count ?? 0;
        var excluded = groups.FirstOrDefault(g => g.Status == PaymentLineStatus.Excluded)?.Count ?? 0;
        return new PaymentObligationLineCounts(pending, paid, excluded);
    }

    public async Task<PaymentObligationLineAggregates> GetLineAggregatesAsync(int obligationId, CancellationToken ct = default)
    {
        var lines = await _context.Set<PaymentObligationLine>().AsNoTracking()
            .Where(l => l.PaymentObligationId == obligationId)
            .Select(l => new { l.LineStatus, l.PaidAmount, l.TotalAmount })
            .ToListAsync(ct);

        var paid = lines.Where(l => l.LineStatus == PaymentLineStatus.Paid).Sum(l => l.PaidAmount ?? 0m);
        var excluded = lines.Where(l => l.LineStatus == PaymentLineStatus.Excluded).Sum(l => l.TotalAmount);
        return new PaymentObligationLineAggregates(paid, excluded);
    }

    // ===== Evidence =====

    public async Task AddEvidenceAsync(PaymentObligationEvidence evidence, CancellationToken ct = default)
    {
        await _context.Set<PaymentObligationEvidence>().AddAsync(evidence, ct);
    }

    public Task<PaymentObligationEvidence?> GetEvidenceByIdAsync(int evidenceId, bool asNoTracking, CancellationToken ct = default)
    {
        var q = _context.Set<PaymentObligationEvidence>()
            .Include(e => e.PaymentObligation)
            .AsQueryable();
        if (asNoTracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(e => e.Id == evidenceId, ct);
    }

    public async Task<IReadOnlyList<PaymentObligationEvidence>> ListEvidenceByObligationAsync(int obligationId, CancellationToken ct = default)
    {
        return await _context.Set<PaymentObligationEvidence>().AsNoTracking()
            .Where(e => e.PaymentObligationId == obligationId)
            .OrderByDescending(e => e.UploadedAt)
            .ToListAsync(ct);
    }

    public Task<int> CountEvidenceAsync(int obligationId, CancellationToken ct = default)
    {
        return _context.Set<PaymentObligationEvidence>().AsNoTracking()
            .CountAsync(e => e.PaymentObligationId == obligationId, ct);
    }

    public void RemoveEvidence(PaymentObligationEvidence evidence)
    {
        _context.Set<PaymentObligationEvidence>().Remove(evidence);
    }

    // ===== Common =====

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
