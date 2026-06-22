using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IAttendanceRecordRepository
{
    /// <summary>
    /// Lookup theo natural key (emp, date, shift). 1 nhân viên có thể có nhiều ca / ngày
    /// sau multi-shift refactor — key bắt buộc bao gồm ShiftTemplateId.
    /// </summary>
    Task<AttendanceRecord?> GetByKeyAsync(
        int employeeInformationId,
        DateOnly workDate,
        int shiftTemplateId,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy tất cả record cho (emp, date) — có thể nhiều khi NV có nhiều ca trong ngày.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeAndDateAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    Task<AttendanceRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeRangeAsync(
        int employeeInformationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AttendanceRecord> Items, int Total)> GetSummaryPagedAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? employeeInformationId,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecord>> GetByDateAsync(DateOnly workDate, CancellationToken cancellationToken = default);

    Task<int> AddAsync(AttendanceRecord entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(AttendanceRecord entity, CancellationToken cancellationToken = default);
}
