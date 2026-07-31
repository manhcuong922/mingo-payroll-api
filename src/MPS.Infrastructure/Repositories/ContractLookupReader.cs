using Microsoft.EntityFrameworkCore;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

/// <summary>
/// Read-only typed lookups required by Contract handlers. Lets the Application layer
/// validate FKs and project lightweight snapshots without ever touching EF Core.
/// </summary>
public sealed class ContractLookupReader : IContractLookupReader
{
    private readonly AppDbContext _context;

    public ContractLookupReader(AppDbContext context)
    {
        _context = context;
    }

    public Task<UserSnapshot?> GetUserSnapshotAsync(int userId, CancellationToken cancellationToken) =>
        _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserSnapshot(u.Id, u.Status, u.Dob, u.FullName))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<EmployeeInfoSnapshot?> GetEmployeeInfoSnapshotAsync(int employeeInformationId, CancellationToken cancellationToken) =>
        _context.EmployeeInformations
            .AsNoTracking()
            .Where(e => e.Id == employeeInformationId)
            .Select(e => new EmployeeInfoSnapshot(e.Id, e.Dob, e.FullName))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int?> GetEmployeeInformationIdByUserIdAsync(int userId, CancellationToken cancellationToken) =>
        _context.EmployeeInformations
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<EmployeeServiceWindow?> GetEmployeeServiceWindowAsync(
        int employeeInformationId, CancellationToken cancellationToken)
    {
        var rows = await QueryServiceWindows(
            c => c.EmployeeInformationId == employeeInformationId).ToListAsync(cancellationToken);
        if (rows.Count == 0) return null;

        var windows = await AttachDepartmentsAsync(rows, cancellationToken);
        return windows[0];
    }

    public async Task<IReadOnlyList<EmployeeServiceWindow>> GetAllEmployeeServiceWindowsAsync(
        CancellationToken cancellationToken)
    {
        var windows = await QueryServiceWindows(_ => true).ToListAsync(cancellationToken);
        return await AttachDepartmentsAsync(windows, cancellationToken);
    }

    /// <summary>
    /// MIN(EffectiveFrom) / MAX(EffectiveTo) trên các HĐ chưa huỷ. Một HĐ không xác định thời hạn
    /// (EffectiveTo = null) làm cả khoảng thành vô hạn ⇒ <c>OpenEnded</c>.
    /// </summary>
    private IQueryable<ServiceWindowRow> QueryServiceWindows(
        System.Linq.Expressions.Expression<Func<Domain.Entities.Contract, bool>> filter) =>
        _context.Contracts
            .AsNoTracking()
            .Where(c => c.Status != ContractStatus.Cancelled)
            .Where(filter)
            .GroupBy(c => c.EmployeeInformationId)
            .Select(g => new ServiceWindowRow(
                g.Key,
                g.Min(c => c.EffectiveFrom),
                g.Max(c => c.EffectiveTo),
                g.Any(c => c.EffectiveTo == null)));

    private async Task<IReadOnlyList<EmployeeServiceWindow>> AttachDepartmentsAsync(
        IReadOnlyList<ServiceWindowRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return Array.Empty<EmployeeServiceWindow>();

        var ids = rows.Select(r => r.EmployeeInformationId).ToList();
        var departmentByEmployee = await _context.EmployeeInformations
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.DepartmentId })
            .ToDictionaryAsync(x => x.Id, x => x.DepartmentId, cancellationToken);

        return rows
            .Select(r => new EmployeeServiceWindow(
                r.EmployeeInformationId,
                r.JoinDate,
                r.OpenEnded ? null : r.ServiceEnd,
                departmentByEmployee.TryGetValue(r.EmployeeInformationId, out var d) ? d : null))
            .ToList();
    }

    private sealed record ServiceWindowRow(
        int EmployeeInformationId, DateTime JoinDate, DateTime? ServiceEnd, bool OpenEnded);

    public Task<bool> IsActiveUserAsync(int userId, CancellationToken cancellationToken) =>
        _context.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Status == 1, cancellationToken);

    public Task<bool> IsActiveContractPartyAsync(int partyId, CancellationToken cancellationToken) =>
        _context.ContractParties.AsNoTracking()
            .AnyAsync(p => p.Id == partyId && p.EffectiveTo == null, cancellationToken);

    public Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken) =>
        _context.Positions.AsNoTracking().AnyAsync(p => p.Id == positionId, cancellationToken);

    public Task<SalaryGradeSnapshot?> GetSalaryGradeSnapshotAsync(int salaryGradeId, CancellationToken cancellationToken) =>
        _context.SalaryGrades
            .AsNoTracking()
            .Where(s => s.Id == salaryGradeId)
            .Select(s => new SalaryGradeSnapshot(s.Id, s.BaseSalary, s.Currency, s.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetActiveAllowanceTypeIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return Array.Empty<int>();
        return await _context.AllowanceTypes
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.IsActive)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsActiveAllowanceTypeAsync(int allowanceTypeId, CancellationToken cancellationToken) =>
        _context.AllowanceTypes.AsNoTracking()
            .AnyAsync(a => a.Id == allowanceTypeId && a.IsActive, cancellationToken);

    public Task<TerminationReasonRefSnapshot?> GetTerminationReasonRefAsync(int reasonRefId, CancellationToken cancellationToken) =>
        _context.ContractTerminationReasonRefs
            .AsNoTracking()
            .Where(r => r.Id == reasonRefId)
            .Select(r => new TerminationReasonRefSnapshot(r.Id, r.Code, r.RequiresWrittenNotice))
            .FirstOrDefaultAsync(cancellationToken);
}
