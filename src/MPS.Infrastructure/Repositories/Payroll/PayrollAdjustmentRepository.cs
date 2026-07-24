using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Payroll;

public sealed class PayrollAdjustmentRepository : IPayrollAdjustmentRepository
{
    private readonly AppDbContext _context;
    public PayrollAdjustmentRepository(AppDbContext context) => _context = context;

    // Tracked (không AsNoTracking): handler tạo kỳ gán AppliedPeriod rồi lưu chung transaction với kỳ.
    public async Task<IReadOnlyList<PayrollAdjustment>> GetPendingForEmployeesAsync(
        IReadOnlyCollection<int> employeeInformationIds, CancellationToken ct = default)
    {
        if (employeeInformationIds.Count == 0) return Array.Empty<PayrollAdjustment>();

        return await _context.PayrollAdjustments
            .Where(a => a.AppliedPeriodId == null
                        && employeeInformationIds.Contains(a.EmployeeInformationId))
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PayrollAdjustment>> GetAppliedToPeriodAsync(
        int periodId, CancellationToken ct = default) =>
        await _context.PayrollAdjustments
            .Where(a => a.AppliedPeriodId == periodId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PayrollAdjustment>> GetPendingFromSourcePeriodAsync(
        int sourcePeriodId, int employeeInformationId, CancellationToken ct = default) =>
        await _context.PayrollAdjustments
            .Where(a => a.AppliedPeriodId == null
                        && a.SourcePeriodId == sourcePeriodId
                        && a.EmployeeInformationId == employeeInformationId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PayrollAdjustment>> GetPendingSourcedByPeriodAsync(
        int sourcePeriodId, CancellationToken ct = default) =>
        await _context.PayrollAdjustments
            .Where(a => a.AppliedPeriodId == null && a.SourcePeriodId == sourcePeriodId)
            .ToListAsync(ct);

    public void Add(PayrollAdjustment adjustment) => _context.PayrollAdjustments.Add(adjustment);

    public void Remove(PayrollAdjustment adjustment) => _context.PayrollAdjustments.Remove(adjustment);

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
