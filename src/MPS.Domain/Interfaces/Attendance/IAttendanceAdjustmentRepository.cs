using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IAttendanceAdjustmentRepository
{
    Task<(IReadOnlyList<AttendanceAdjustment> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        AdjustmentStatus? status,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<AttendanceAdjustment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AttendanceAdjustment?> GetPendingByRecordIdAsync(int attendanceRecordId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(AttendanceAdjustment entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(AttendanceAdjustment entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// FT-14: atomically approve a pending adjustment.
    /// Within a single transaction:
    /// 1. Snapshots the current record fields into adjustment.Original*.
    /// 2. Overwrites the AttendanceRecord with adjustment.New*.
    /// 3. Marks adjustment as Approved + audit.
    /// </summary>
    Task<bool> ApproveAndApplyAsync(
        int adjustmentId,
        int reviewedByUserId,
        string? reviewNote,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FT-14 (HR override path): direct override of an attendance record without a workflow.
    /// Creates an AttendanceAdjustment row with Status=Approved (audit trail) AND overwrites
    /// the AttendanceRecord. Requires AttendanceAdjustment.Override.All permission at the caller.
    /// </summary>
    Task<int> OverrideAndApplyAsync(
        Domain.Entities.Attendance.AttendanceAdjustment adjustment,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}