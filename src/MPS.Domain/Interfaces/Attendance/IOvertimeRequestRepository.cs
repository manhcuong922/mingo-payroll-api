using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IOvertimeRequestRepository
{
    Task<(IReadOnlyList<OvertimeRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        OtRequestStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? createdByUserId,
        int? projectId,
        // v3: PM inbox — filter theo Project.ResponsibleUserId. Query join Project.
        int? projectResponsibleUserId,
        int page,
        int size,
        // v3: Approver auto-filter theo permission. HR chỉ thấy route=Hr; DM chỉ thấy
        // route=DepartmentManager + Project.DepartmentId khớp dept của DM.
        OvertimeApproverRoute? approverRoute = null,
        int? projectDepartmentId = null,
        CancellationToken cancellationToken = default);

    Task<OvertimeRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sum OtHours của các đơn khớp status. Mặc định (statuses=null) chỉ Approved
    /// — dùng cho báo cáo tuân thủ. CapChecker pre-submit truyền [Pending, PendingApprover, Approved]
    /// để chặn NV lạm dụng gửi nhiều đơn Pending vượt cap trước khi HR duyệt.
    /// </summary>
    Task<decimal> GetTotalOtHoursInMonthAsync(
        int employeeInformationId,
        int year,
        int month,
        IReadOnlyCollection<OtRequestStatus>? statuses = null,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalOtHoursInYearAsync(
        int employeeInformationId,
        int year,
        IReadOnlyCollection<OtRequestStatus>? statuses = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sum OtHours các đơn khớp status của NV trên 1 workDate cụ thể, có thể exclude 1 id
    /// (để bỏ qua chính đơn đang sửa). Dùng ở CapChecker cho 50% ca chính.
    /// </summary>
    Task<decimal> GetTotalOtHoursOnDateAsync(
        int employeeInformationId,
        DateOnly workDate,
        IReadOnlyCollection<OtRequestStatus> statuses,
        int? excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sum OtHours theo tháng cho toàn bộ năm — trả về mảng 12 phần tử (index 0 = tháng 1).
    /// Dùng ở panel NV "OT tháng/năm này". 1 SQL query, group theo Month.
    /// </summary>
    Task<IReadOnlyList<decimal>> GetMonthlyOtHoursInYearAsync(
        int employeeInformationId,
        int year,
        IReadOnlyCollection<OtRequestStatus> statuses,
        CancellationToken cancellationToken = default);

    Task<int> AddAsync(OvertimeRequest entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(OvertimeRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// OT Approved trên ngày X mà chưa có AttendanceRecord tương ứng — HR chấm cho các OT này.
    /// Include EmployeeInformation + Project.
    /// </summary>
    Task<IReadOnlyList<OvertimeRequest>> GetApprovedWithoutAttendanceOnDateAsync(
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load các OT ACTIVE (Pending + PendingApprover + Approved, KHÔNG bao gồm Rejected/Cancelled)
    /// của 1 NV trên 1 workDate — để check trùng khung giờ khi NV tạo/sửa đơn mới. excludeId để
    /// exclude chính đơn đang sửa (bỏ qua conflict với chính mình).
    /// </summary>
    Task<IReadOnlyList<OvertimeRequest>> GetActiveOnDateForEmployeeAsync(
        int employeeInformationId,
        DateOnly workDate,
        int? excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// #41 — Load các OT ACTIVE của 1 NV có WorkDate trong khoảng [fromDate, toDate], để check trùng
    /// khung giờ CÓ XÉT OT vắt nửa đêm: đơn 22:00→02:00 lưu WorkDate=D nhưng chồng giờ với đơn của
    /// D+1. Guard trùng-giờ lúc tạo/sửa phải quét D±1 (giống <c>OvertimeCapChecker</c>), không chỉ D.
    /// </summary>
    Task<IReadOnlyList<OvertimeRequest>> GetActiveInRangeForEmployeeAsync(
        int employeeInformationId,
        DateOnly fromDate,
        DateOnly toDate,
        int? excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả về true nếu tồn tại OT ACTIVE (Pending / PendingApprover / Approved) của employee
    /// có WorkDate rơi vào bất kỳ ngày nào trong khoảng [startDate, endDate].
    /// Dùng cho cross-check chống trùng ngày giữa Leave/WFH/LateEarly và OT.
    /// </summary>
    Task<bool> HasActiveInRangeAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        int? excludeRequestId,
        CancellationToken cancellationToken = default);
}