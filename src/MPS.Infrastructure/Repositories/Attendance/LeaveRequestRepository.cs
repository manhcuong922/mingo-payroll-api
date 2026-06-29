using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly AppDbContext _context;

    public LeaveRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<LeaveRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        LeaveRequestStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.LeaveType)
            .AsQueryable();

        if (employeeInformationId.HasValue)
            query = query.Where(r => r.EmployeeInformationId == employeeInformationId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        if (fromDate.HasValue)
            query = query.Where(r => r.EndDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(r => r.StartDate <= toDate.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.StartDate)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<LeaveRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.LeaveRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<int> AddAsync(LeaveRequest entity, CancellationToken cancellationToken = default)
    {
        await _context.LeaveRequests.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(LeaveRequest entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.LeaveRequests.FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Status = entity.Status;
        tracked.ReviewNote = entity.ReviewNote;
        tracked.ReviewedByUserId = entity.ReviewedByUserId;
        tracked.ReviewedAt = entity.ReviewedAt;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAndApplyAsync(
        int requestId,
        int reviewedByUserId,
        string? reviewNote,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var request = await _context.LeaveRequests
            .Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        if (request.Status != LeaveRequestStatus.Pending)
            throw new DomainValidationException(
                $"Leave request {requestId} cannot be approved — current status is {request.Status}.");

        if (request.CreatedByUserId == reviewedByUserId)
            throw new DomainValidationException("BR-10: Self-approval is forbidden.");

        // FT-13: deduct balance only when the leave type is configured to do so (vd Annual)
        if (request.LeaveType.DeductsBalance)
        {
            var balance = await _context.LeaveBalances.FirstOrDefaultAsync(
                b => b.EmployeeInformationId == request.EmployeeInformationId
                  && b.Year == request.StartDate.Year
                  && b.LeaveTypeId == request.LeaveTypeId,
                cancellationToken);

            if (balance is null)
                throw new DomainValidationException(
                    $"No '{request.LeaveType.Code}' balance exists for employee {request.EmployeeInformationId} in {request.StartDate.Year}.");

            var remaining = balance.TotalDays - balance.UsedDays;
            if (remaining < request.TotalDays)
                throw new DomainValidationException(
                    $"BR-7: Insufficient '{request.LeaveType.Code}' balance. Remaining={remaining}, requested={request.TotalDays}.");

            balance.UsedDays += request.TotalDays;
            balance.UpdatedAt = nowUtc;
            balance.UpdatedByUserId = reviewedByUserId;
        }

        // Upsert AttendanceRecord (Status=OnLeave) for each day in the range
        var existingRecords = await _context.AttendanceRecords
            .Where(r => r.EmployeeInformationId == request.EmployeeInformationId
                && r.WorkDate >= request.StartDate
                && r.WorkDate <= request.EndDate)
            .ToListAsync(cancellationToken);

        var existingByDate = existingRecords.ToDictionary(r => r.WorkDate);

        for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
        {
            if (existingByDate.TryGetValue(date, out var existing))
            {
                existing.Status = AttendanceStatus.OnLeave;
                existing.DayHours = 0m;
                existing.NightHours = 0m;
                existing.Notes = $"Auto-applied from approved leave request #{request.Id}";
                existing.UpdatedAt = nowUtc;
                existing.UpdatedByUserId = reviewedByUserId;
            }
            else
            {
                await _context.AttendanceRecords.AddAsync(new AttendanceRecord
                {
                    EmployeeInformationId = request.EmployeeInformationId,
                    WorkDate = date,
                    Status = AttendanceStatus.OnLeave,
                    DayHours = 0m,
                    NightHours = 0m,
                    Notes = $"Auto-applied from approved leave request #{request.Id}",
                    CreatedAt = nowUtc,
                    CreatedByUserId = reviewedByUserId
                }, cancellationToken);
            }
        }

        request.Status = LeaveRequestStatus.Approved;
        request.ReviewedByUserId = reviewedByUserId;
        request.ReviewedAt = nowUtc;
        request.ReviewNote = reviewNote;
        request.UpdatedAt = nowUtc;
        request.UpdatedByUserId = reviewedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }
}
