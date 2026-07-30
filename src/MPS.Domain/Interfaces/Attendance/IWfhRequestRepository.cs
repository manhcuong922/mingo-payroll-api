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
        int? departmentIdFilter = null,
        OvertimeApproverRoute? approverRouteFilter = null,
        CancellationToken cancellationToken = default);

    Task<WfhRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(WfhRequest entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(WfhRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// BS-4 / [D11] — duyệt đơn WFH và SINH BẢN GHI CHẤM CÔNG cho từng ngày đã phân ca trong khoảng.
    /// WFH là ngày ĐI LÀM (tại nhà) nên bản ghi ở trạng thái <c>Present</c> với đủ giờ chuẩn của ca;
    /// trước đây duyệt WFH không tạo bản ghi nào ⇒ ngày đó bị coi như không có công khi tính lương.
    /// Cập nhật trạng thái đơn + upsert AttendanceRecord trong CÙNG transaction.
    /// </summary>
    Task<bool> ApproveAndApplyAsync(
        int requestId,
        int reviewedByUserId,
        string? reviewNote,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Trả về true nếu tồn tại đơn WFH Pending hoặc Approved của employee
    /// chồng lấp bất kỳ ngày nào trong khoảng [startDate, endDate].
    /// Dùng cho cross-check chống trùng ngày giữa Leave/WFH/LateEarly.
    /// </summary>
    Task<bool> HasPendingOrApprovedInRangeAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        int? excludeRequestId,
        CancellationToken cancellationToken = default);
}
