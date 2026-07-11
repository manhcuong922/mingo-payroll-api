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

    public async Task<IReadOnlyList<EmployeeLeaveMonthlySummary>> GetYearlyEmployeeSummaryAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var requests = await _context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.StartDate <= yearEnd
                && r.EndDate >= yearStart
                && (r.Status == LeaveRequestStatus.Approved || r.Status == LeaveRequestStatus.Pending))
            .Select(r => new
            {
                r.EmployeeInformationId,
                r.StartDate,
                r.EndDate,
                r.Status
            })
            .ToListAsync(cancellationToken);

        var buckets = new Dictionary<(int EmployeeInformationId, int Month), SummaryBucket>();

        foreach (var request in requests)
        {
            for (var month = 1; month <= 12; month++)
            {
                var monthStart = new DateOnly(year, month, 1);
                var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
                var start = request.StartDate > monthStart ? request.StartDate : monthStart;
                var end = request.EndDate < monthEnd ? request.EndDate : monthEnd;
                if (end < start) continue;

                var days = CountWorkingDays(start, end);
                if (days <= 0m) continue;

                var key = (request.EmployeeInformationId, month);
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new SummaryBucket();
                    buckets[key] = bucket;
                }

                if (request.Status == LeaveRequestStatus.Approved)
                {
                    bucket.ApprovedDays += days;
                    bucket.ApprovedRequestCount += 1;
                }
                else if (request.Status == LeaveRequestStatus.Pending)
                {
                    bucket.PendingDays += days;
                    bucket.PendingRequestCount += 1;
                }
            }
        }

        return buckets
            .OrderBy(item => item.Key.EmployeeInformationId)
            .ThenBy(item => item.Key.Month)
            .Select(item => new EmployeeLeaveMonthlySummary(
                item.Key.EmployeeInformationId,
                item.Key.Month,
                item.Value.ApprovedDays,
                item.Value.ApprovedRequestCount,
                item.Value.PendingDays,
                item.Value.PendingRequestCount))
            .ToList();
    }

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
        tracked.MedicalCertificateUrl = entity.MedicalCertificateUrl;
        tracked.InsuranceCertNumber = entity.InsuranceCertNumber;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelAsync(
        int requestId,
        int cancelledByUserId,
        string? cancelReason,
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

        if (request.CreatedByUserId != cancelledByUserId)
            throw new ForbiddenAccessException("Chỉ người tạo đơn mới được hủy.");

        var today = DateOnly.FromDateTime(nowUtc.AddHours(7));

        switch (request.Status)
        {
            case LeaveRequestStatus.Pending:
                // OK — no compensation needed
                break;

            case LeaveRequestStatus.Approved:
                if (request.StartDate <= today)
                    throw new DomainValidationException(
                        "Đơn đã duyệt chỉ được hủy trước ngày bắt đầu nghỉ.");

                await RevertApprovedLeaveAsync(request, cancelledByUserId, nowUtc, cancellationToken);
                break;

            default:
                throw new DomainValidationException(
                    $"Không thể hủy đơn ở trạng thái {request.Status}.");
        }

        request.Status = LeaveRequestStatus.Cancelled;
        request.CancelledAt = nowUtc;
        request.CancelledByUserId = cancelledByUserId;
        request.CancelReason = cancelReason;
        request.UpdatedAt = nowUtc;
        request.UpdatedByUserId = cancelledByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    private async Task RevertApprovedLeaveAsync(
        LeaveRequest request,
        int userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // Revert LeaveBalance if the type deducts balance.
        var tracksBalance = request.LeaveType.DeductsBalance || request.LeaveType.MaxDaysPerYear.HasValue;
        if (tracksBalance)
        {
            var balance = await _context.LeaveBalances.FirstOrDefaultAsync(
                b => b.EmployeeInformationId == request.EmployeeInformationId
                  && b.Year == request.StartDate.Year
                  && b.LeaveTypeId == request.LeaveTypeId,
                cancellationToken);
            if (balance is not null)
            {
                balance.UsedDays = Math.Max(0m, balance.UsedDays - request.TotalDays);
                balance.UpdatedAt = nowUtc;
                balance.UpdatedByUserId = userId;
            }
        }

        // Remove auto-created OnLeave attendance records for the range.
        var autoNote = $"Auto-applied from approved leave request #{request.Id}";
        var records = await _context.AttendanceRecords
            .Where(r => r.EmployeeInformationId == request.EmployeeInformationId
                && r.WorkDate >= request.StartDate
                && r.WorkDate <= request.EndDate
                && r.Status == AttendanceStatus.OnLeave
                && r.Notes == autoNote)
            .ToListAsync(cancellationToken);
        _context.AttendanceRecords.RemoveRange(records);
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

        // FT-13: leave types with a yearly quota are tracked in LeaveBalances.
        var tracksBalance = request.LeaveType.DeductsBalance || request.LeaveType.MaxDaysPerYear.HasValue;
        if (tracksBalance)
        {
            var balance = await _context.LeaveBalances.FirstOrDefaultAsync(
                b => b.EmployeeInformationId == request.EmployeeInformationId
                  && b.Year == request.StartDate.Year
                  && b.LeaveTypeId == request.LeaveTypeId,
                cancellationToken);

            var yearlyQuota = request.LeaveType.MaxDaysPerYear ?? request.LeaveType.MaxDaysPerRequest ?? 0m;
            if (balance is null)
            {
                if (yearlyQuota <= 0m)
                    throw new DomainValidationException(
                        $"No '{request.LeaveType.Code}' balance exists for employee {request.EmployeeInformationId} in {request.StartDate.Year}.");

                balance = new LeaveBalance
                {
                    EmployeeInformationId = request.EmployeeInformationId,
                    Year = request.StartDate.Year,
                    LeaveTypeId = request.LeaveTypeId,
                    BaseDays = yearlyQuota,
                    SeniorityBonusDays = 0m,
                    TotalDays = yearlyQuota,
                    UsedDays = 0m,
                    CreatedAt = nowUtc,
                    CreatedByUserId = reviewedByUserId
                };
                await _context.LeaveBalances.AddAsync(balance, cancellationToken);
            }
            else if (balance.TotalDays <= 0m && yearlyQuota > 0m)
            {
                balance.BaseDays = yearlyQuota;
                balance.SeniorityBonusDays = 0m;
                balance.TotalDays = yearlyQuota;
                balance.UpdatedAt = nowUtc;
                balance.UpdatedByUserId = reviewedByUserId;
            }

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

    private static decimal CountWorkingDays(DateOnly start, DateOnly end)
    {
        var count = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Sunday) count++;
        }
        return count;
    }

    private sealed class SummaryBucket
    {
        public decimal ApprovedDays { get; set; }
        public int ApprovedRequestCount { get; set; }
        public decimal PendingDays { get; set; }
        public int PendingRequestCount { get; set; }
    }
}
