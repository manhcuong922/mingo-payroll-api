using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class LeaveBalanceRepository : ILeaveBalanceRepository
{
    private readonly AppDbContext _context;

    public LeaveBalanceRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<LeaveBalance?> GetByKeyAsync(int employeeInformationId, int year, int leaveTypeId, CancellationToken cancellationToken = default) =>
        _context.LeaveBalances
            .AsNoTracking()
            .Include(b => b.LeaveType)
            .FirstOrDefaultAsync(b => b.EmployeeInformationId == employeeInformationId
                && b.Year == year
                && b.LeaveTypeId == leaveTypeId, cancellationToken);

    public async Task<IReadOnlyList<LeaveBalance>> GetByEmployeeAsync(int employeeInformationId, int year, CancellationToken cancellationToken = default)
    {
        return await _context.LeaveBalances
            .AsNoTracking()
            .Include(b => b.LeaveType)
            .Where(b => b.EmployeeInformationId == employeeInformationId && b.Year == year)
            .OrderBy(b => b.LeaveType.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> AddAsync(LeaveBalance entity, CancellationToken cancellationToken = default)
    {
        await _context.LeaveBalances.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(LeaveBalance entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.LeaveBalances.FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.BaseDays = entity.BaseDays;
        tracked.SeniorityBonusDays = entity.SeniorityBonusDays;
        tracked.TotalDays = entity.TotalDays;
        tracked.UsedDays = entity.UsedDays;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpsertForLeaveTypeAsync(
        int leaveTypeId,
        int year,
        IReadOnlyCollection<int> employeeInformationIds,
        decimal totalDays,
        int userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var targetIds = employeeInformationIds.Distinct().ToArray();
        if (targetIds.Length == 0) return;

        var existing = await _context.LeaveBalances
            .Where(b => b.LeaveTypeId == leaveTypeId
                && b.Year == year
                && targetIds.Contains(b.EmployeeInformationId))
            .ToListAsync(cancellationToken);

        foreach (var balance in existing)
        {
            balance.BaseDays = totalDays;
            balance.SeniorityBonusDays = 0m;
            balance.TotalDays = totalDays;
            balance.UpdatedAt = nowUtc;
            balance.UpdatedByUserId = userId;
        }

        var existingEmployeeIds = existing.Select(b => b.EmployeeInformationId).ToHashSet();
        var missingBalances = targetIds
            .Where(id => !existingEmployeeIds.Contains(id))
            .Select(id => new LeaveBalance
            {
                EmployeeInformationId = id,
                Year = year,
                LeaveTypeId = leaveTypeId,
                BaseDays = totalDays,
                SeniorityBonusDays = 0m,
                TotalDays = totalDays,
                UsedDays = 0m,
                CreatedAt = nowUtc,
                CreatedByUserId = userId
            })
            .ToList();

        if (missingBalances.Count > 0)
            await _context.LeaveBalances.AddRangeAsync(missingBalances, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
