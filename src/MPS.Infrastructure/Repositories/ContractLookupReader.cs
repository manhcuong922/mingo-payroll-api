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

    public async Task<DateTime?> GetEmployeeJoinDateAsync(int employeeInformationId, CancellationToken cancellationToken)
    {
        var dates = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.EmployeeInformationId == employeeInformationId
                && c.Status != ContractStatus.Cancelled)
            .Select(c => c.EffectiveFrom)
            .ToListAsync(cancellationToken);
        return dates.Count == 0 ? null : dates.Min();
    }

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
