using MediatR;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Common.Models.Notifications;

/// <summary>
/// Phát khi 1 đơn (Leave/OT/LateEarly/WFH/Adjustment/Dependent) vừa được tạo (Phase 3).
/// Template <c>AttendanceRequestSubmittedTemplate</c> gửi tới danh sách người duyệt trong
/// <see cref="ApproverUserIds"/> — đã được handler resolve QUA <c>IAttendanceApproverResolver</c>
/// (chính là nhóm sẽ được phép bấm nút Approve tại thời điểm này). KHÔNG broadcast toàn permission
/// — nếu route = DepartmentManager thì chỉ DM đúng phòng ban NV nhận, không phải mọi DM.
///
/// <see cref="SubmittedByUserId"/> = người bấm nút submit (có thể là NV tự nộp, có thể HR nộp hộ);
/// <see cref="RequesterUserId"/> = NV chủ đơn (thường trùng submit, khác khi nộp hộ);
/// <see cref="Summary"/> = tóm tắt hiển thị trong TB (VD "3 ngày phép năm từ 05/08 tới 07/08").
/// </summary>
public sealed record AttendanceRequestSubmittedNotification(
    AttendanceRequestKind Kind,
    int RequestId,
    int SubmittedByUserId,
    int RequesterUserId,
    IReadOnlyList<int> ApproverUserIds,
    string Summary) : INotification;

/// <summary>
/// Phát khi 1 đơn được duyệt/từ chối (Phase 3). Template gửi tới người submit (+ NV chủ đơn nếu khác).
/// </summary>
public sealed record AttendanceRequestDecidedNotification(
    AttendanceRequestKind Kind,
    int RequestId,
    AttendanceRequestDecision Decision,
    int DecidedByUserId,
    int SubmittedByUserId,
    int RequesterUserId,
    string Summary,
    string? Reason) : INotification;
