using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IWfhRequestRepository
{
    Task<(IReadOnlyList<WfhRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        WfhRequestStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<WfhRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(WfhRequest entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(WfhRequest entity, CancellationToken cancellationToken = default);

    /// <summary>Sum(TotalDays) trong tháng cho các đơn Approved của 1 employee.</summary>
    Task<decimal> SumApprovedDaysInMonthAsync(
        int employeeInformationId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm overlap trên khoảng [startDate, endDate] với đơn WFH khác (Pending/Approved).
    /// </summary>
    Task<bool> HasOverlappingWfhAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        int? excludeRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm overlap với LeaveRequest Approved cùng khoảng — không cho vừa nghỉ vừa WFH.
    /// </summary>
    Task<bool> HasOverlappingLeaveAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
