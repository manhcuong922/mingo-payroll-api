using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Resolve danh sách user id ĐỦ TƯ CÁCH DUYỆT đơn tại thời điểm submit — dùng cho notification
/// "có đơn mới cần duyệt". Logic bám theo <see cref="ApproverDepartmentGuard"/> +
/// <c>ApproveOvertimeRequestCommandHandler.EnsureApproverRouteMatches</c> (nguồn sự thật cho auth
/// khi thực sự bấm nút Approve) để đảm bảo TB bay tới CHÍNH nhóm được phép bấm nút.
///
/// Quy tắc theo <see cref="AttendanceRequestKind"/>:
///   • <b>Leave / LateEarly / WFH</b>: theo <c>ApproverRoute</c> gắn trên đơn.
///     - <c>Hr</c>       → mọi user role HR (cross-department).
///     - <c>Director</c> → user role Director.
///     - <c>DepartmentManager</c> → DM CÙNG phòng ban với NV tạo đơn (giao 2 tập: role DM ∩ member dept đó).
///   • <b>Overtime</b>: submit lần đầu (Status=Pending) → PM đơn (<c>Project.ResponsibleUserId</c>).
///     Sau khi PM Forward (Status=PendingApprover) — event submit KHÔNG re-publish nữa, nên nhánh này
///     KHÔNG resolve ở đây (Approve tier 2 nếu cần TB "gần đến lượt" sẽ có event riêng).
///   • <b>Adjustment</b>: mọi user có permission <c>AttendanceAdjustment.Approve.All</c>.
///   • <b>Dependent</b>: mọi user có permission <c>DependentRequest.Approve.All</c>.
///
/// Loại người submit ra khỏi danh sách (BR-10 self-review) — kể cả họ có quyền duyệt.
/// </summary>
public interface IAttendanceApproverResolver
{
    /// <param name="tierOneApproverUserId">Chỉ dùng cho OT — user id PM đơn (=Project.ResponsibleUserId).</param>
    /// <param name="excludeUserId">Người submit — tránh tự report cho chính mình.</param>
    Task<IReadOnlyList<int>> ResolveForSubmitAsync(
        AttendanceRequestKind kind,
        OvertimeApproverRoute? approverRoute,
        int employeeInformationId,
        int? tierOneApproverUserId,
        int? excludeUserId,
        CancellationToken ct);
}
