using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class AttendanceAdjustmentRepository : IAttendanceAdjustmentRepository
{
    private readonly AppDbContext _context;
    private readonly IWorkHourSplitter _splitter;

    public AttendanceAdjustmentRepository(AppDbContext context, IWorkHourSplitter splitter)
    {
        _context = context;
        _splitter = splitter;
    }

    public async Task<(IReadOnlyList<AttendanceAdjustment> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        AdjustmentStatus? status,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AttendanceAdjustments
            .AsNoTracking()
            .Include(a => a.EmployeeInformation)
            .Include(a => a.AttendanceRecord)
            .AsQueryable();

        if (employeeInformationId.HasValue)
            query = query.Where(a => a.EmployeeInformationId == employeeInformationId.Value);
        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<AttendanceAdjustment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.AttendanceAdjustments
            .AsNoTracking()
            .Include(a => a.EmployeeInformation)
            .Include(a => a.AttendanceRecord)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<AttendanceAdjustment?> GetPendingByRecordIdAsync(int attendanceRecordId, CancellationToken cancellationToken = default) =>
        _context.AttendanceAdjustments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AttendanceRecordId == attendanceRecordId && a.Status == AdjustmentStatus.Pending, cancellationToken);

    public async Task<int> AddAsync(AttendanceAdjustment entity, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceAdjustments.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(AttendanceAdjustment entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.AttendanceAdjustments.FirstOrDefaultAsync(a => a.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Status = entity.Status;
        tracked.ReviewNote = entity.ReviewNote;
        tracked.ReviewedByUserId = entity.ReviewedByUserId;
        tracked.ReviewedAt = entity.ReviewedAt;
        tracked.OriginalStatus = entity.OriginalStatus;
        tracked.OriginalCheckInTime = entity.OriginalCheckInTime;
        tracked.OriginalCheckOutTime = entity.OriginalCheckOutTime;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAndApplyAsync(
        int adjustmentId,
        int reviewedByUserId,
        string? reviewNote,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var adjustment = await _context.AttendanceAdjustments.FirstOrDefaultAsync(a => a.Id == adjustmentId, cancellationToken);
        if (adjustment is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        if (adjustment.Status != AdjustmentStatus.Pending)
            throw new DomainValidationException(
                $"Adjustment {adjustmentId} cannot be approved — current status is {adjustment.Status}.");

        if (adjustment.CreatedByUserId == reviewedByUserId)
            throw new DomainValidationException("BR-10: Self-approval is forbidden.");

        var record = await _context.AttendanceRecords
            .Include(r => r.ShiftTemplate)
            .FirstOrDefaultAsync(r => r.Id == adjustment.AttendanceRecordId, cancellationToken)
            ?? throw new NotFoundException($"AttendanceRecord {adjustment.AttendanceRecordId} not found.");

        // 1. Snapshot pre-change values for audit trail (DC-07)
        adjustment.OriginalStatus = record.Status;
        adjustment.OriginalCheckInTime = record.CheckInTime;
        adjustment.OriginalCheckOutTime = record.CheckOutTime;

        // 2. Overwrite the attendance record + recompute breakdown.
        record.Status = adjustment.NewStatus;
        record.CheckInTime = adjustment.NewCheckInTime;
        record.CheckOutTime = adjustment.NewCheckOutTime;
        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter, record.Status, record.CheckInTime, record.CheckOutTime, record.ShiftTemplate);
        record.DayHours = dayHours;
        record.NightHours = nightHours;
        record.UpdatedAt = nowUtc;
        record.UpdatedByUserId = reviewedByUserId;

        // 3. Mark adjustment as Approved + audit
        adjustment.Status = AdjustmentStatus.Approved;
        adjustment.ReviewedByUserId = reviewedByUserId;
        adjustment.ReviewedAt = nowUtc;
        adjustment.ReviewNote = reviewNote;
        adjustment.UpdatedAt = nowUtc;
        adjustment.UpdatedByUserId = reviewedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<int> OverrideAndApplyAsync(
        AttendanceAdjustment adjustment,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var record = await _context.AttendanceRecords
            .Include(r => r.ShiftTemplate)
            .FirstOrDefaultAsync(r => r.Id == adjustment.AttendanceRecordId, cancellationToken)
            ?? throw new NotFoundException($"AttendanceRecord {adjustment.AttendanceRecordId} not found.");

        // Snapshot pre-change values into the adjustment record (audit trail)
        adjustment.OriginalStatus = record.Status;
        adjustment.OriginalCheckInTime = record.CheckInTime;
        adjustment.OriginalCheckOutTime = record.CheckOutTime;
        adjustment.Status = AdjustmentStatus.Approved;
        adjustment.ReviewedByUserId = adjustment.CreatedByUserId;
        adjustment.ReviewedAt = nowUtc;

        await _context.AttendanceAdjustments.AddAsync(adjustment, cancellationToken);

        // Overwrite the attendance record + propagate override reason + recompute breakdown.
        record.Status = adjustment.NewStatus;
        record.CheckInTime = adjustment.NewCheckInTime;
        record.CheckOutTime = adjustment.NewCheckOutTime;
        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter, record.Status, record.CheckInTime, record.CheckOutTime, record.ShiftTemplate);
        record.DayHours = dayHours;
        record.NightHours = nightHours;
        record.OverrideReason = adjustment.Reason;
        record.UpdatedAt = nowUtc;
        record.UpdatedByUserId = adjustment.CreatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return adjustment.Id;
    }
}