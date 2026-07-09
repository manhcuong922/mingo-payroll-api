using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IEmployeeShiftAssignmentRepository
{
    /// <summary>
    /// Lookup theo natural key (emp, date, shift). 1 NV có thể có nhiều ca / ngày
    /// sau multi-shift refactor — key bắt buộc bao gồm ShiftTemplateId.
    /// </summary>
    Task<EmployeeShiftAssignment?> GetByKeyAsync(
        int employeeInformationId,
        DateOnly workDate,
        int shiftTemplateId,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy tất cả assignments cho (emp, date) — có thể nhiều.</summary>
    Task<IReadOnlyList<EmployeeShiftAssignment>> GetByEmployeeAndDateAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeShiftAssignment>> GetByDateAsync(
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="includeInactiveTemplates"/>: mặc định <c>true</c> — trả về TẤT CẢ assignments
    /// (dùng cho write-path: batch conflict check, cap checker). Pass <c>false</c> ở display query để
    /// ẩn assignment do template inactive sở hữu (xem §13).
    /// </summary>
    Task<IReadOnlyList<EmployeeShiftAssignment>> GetByEmployeesAndDateRangeAsync(
        IReadOnlyCollection<int> employeeInformationIds,
        DateOnly fromDate,
        DateOnly toDate,
        bool includeInactiveTemplates = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Multi-shift: trả về các (emp, date, shiftId) bị khoá vì đã có AttendanceRecord.
    /// Khoá theo từng ca — NV có 2 ca / ngày chỉ ca đã chấm bị khoá.
    /// </summary>
    Task<IReadOnlyList<(int EmployeeInformationId, DateOnly WorkDate, int ShiftTemplateId)>> GetLockedShiftsAsync(
        IReadOnlyCollection<int> employeeInformationIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task ApplyBatchAsync(
        IReadOnlyCollection<EmployeeShiftAssignment> toAdd,
        IReadOnlyCollection<(int Id, int NewShiftTemplateId, int? NewScheduleTemplateId)> toUpdate,
        IReadOnlyCollection<int> toRemoveIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Multi-shift: trả về các (emp, date) đã có ca thuộc template khác và ca đó
    /// GIAO THOA THỜI GIAN với <paramref name="newShiftStart"/>..<paramref name="newShiftEnd"/>.
    /// Cho phép 2 template apply cùng (emp, date) miễn ca không overlap.
    /// </summary>
    Task<IReadOnlyList<(int EmployeeInformationId, DateOnly WorkDate, int OtherScheduleTemplateId, string OtherScheduleTemplateName, string EmployeeFullName)>>
        GetOwnedConflictsAsync(
            IReadOnlyCollection<(int EmployeeInformationId, DateOnly WorkDate)> pairs,
            int? excludeScheduleTemplateId,
            TimeOnly newShiftStart,
            TimeOnly newShiftEnd,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes assignments owned by the given ScheduleTemplate whose <c>WorkDate</c>
    /// is NOT in <paramref name="retainDates"/> (i.e. dates removed from the pattern
    /// after an update), skipping (emp, date, shift) that have an <c>AttendanceRecord</c>.
    /// </summary>
    Task<int> DeleteStaleByScheduleTemplateAsync(
        int scheduleTemplateId,
        IReadOnlyCollection<DateOnly> retainDates,
        IReadOnlyCollection<int> retainShiftTemplateIds,
        IReadOnlyCollection<int> retainEmployeeInformationIds,
        CancellationToken cancellationToken = default);

    Task<int> DeleteUnlockedByScheduleTemplateAsync(
        int scheduleTemplateId,
        CancellationToken cancellationToken = default);
}
