using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface ILateEarlyRequestRepository
{
    Task<(IReadOnlyList<LateEarlyRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        LateEarlyRequestStatus? status,
        LateEarlyKind? kind,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        int? departmentIdFilter = null,
        OvertimeApproverRoute? approverRouteFilter = null,
        CancellationToken cancellationToken = default);

    Task<LateEarlyRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(LateEarlyRequest entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(LateEarlyRequest entity, CancellationToken cancellationToken = default);

    Task<int> CountApprovedInMonthAsync(
        int employeeInformationId,
        int year,
        int month,
        LateEarlyKind kind,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlapAsync(
        int employeeInformationId,
        DateOnly workDate,
        LateEarlyKind kind,
        int? excludeRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả về true nếu tồn tại đơn LateEarly Pending hoặc Approved của employee
    /// trong khoảng [startDate, endDate] (kiểm mọi Kind).
    /// Dùng cho cross-check chống trùng ngày giữa Leave/WFH/LateEarly.
    /// </summary>
    Task<bool> HasPendingOrApprovedInRangeAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        int? excludeRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tất cả đơn Late/Early đã Approved cho (employee, workDate). Dùng bởi hourly
    /// calculator để cộng trả số phút được miễn vào giờ công của bản ghi chấm công.
    /// </summary>
    Task<IReadOnlyList<LateEarlyRequest>> GetApprovedByEmployeeAndDateAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default);
}
