using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Examples;

/// <summary>
/// ⚠️ TEMPLATE MẪU (tham khảo) — minh hoạ cách viết 1 loại thông báo. CHƯA nối vào sự kiện thật nào,
/// nên hiện không phát ra thông báo. Khi module Ca làm hoàn thiện: đổi payload cho khớp dữ liệu thật,
/// điền đúng người nhận, rồi phát bằng <c>_notifier.RaiseAsync(payload)</c> từ handler tạo ca.
///
/// CÁCH THÊM 1 LOẠI THÔNG BÁO MỚI:
///   1) Tạo 1 record payload (dữ liệu sự kiện).
///   2) Tạo 1 class kế thừa NotificationTemplate&lt;TPayload&gt;, override Type + BuildAsync.
///   3) Trong BuildAsync: chọn NGƯỜI NHẬN bằng RecipientSpec (Users / Roles / Departments / Everyone,
///      kèm .Excluding(...)), và soạn NỘI DUNG bằng Content(...).
///   4) Xong — AddNotificationModule tự đăng ký template; chỉ cần gọi RaiseAsync ở nơi phát sự kiện.
///
/// Template ĐƯỢC PHÉP inject repository (Application interfaces) qua constructor để tra thêm dữ liệu
/// (vd danh sách công nhân trong ca, tên xưởng...). Ví dụ này để tối giản nên không inject gì.
/// </summary>
public sealed class ExampleShiftCreatedTemplate : NotificationTemplate<ExampleShiftCreatedPayload>
{
    public override NotificationType Type => NotificationType.ShiftCreated;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        ExampleShiftCreatedPayload payload, CancellationToken ct)
    {
        // (1) Nhóm nhân viên trong xưởng nhận thông báo — LOẠI TRỪ người tạo ca.
        var employeesContent = Content(
            category: NotificationCategory.Schedule,
            title: "Ca làm mới",
            message: $"Bạn được xếp ca ngày {payload.WorkDate:dd/MM/yyyy}.",
            linkUrl: $"/schedule/shifts/{payload.ShiftId}");

        var employees = Plan(
            RecipientSpec
                .Departments(payload.DepartmentId)          // nhóm người (theo phòng ban/xưởng)
                .ToUsers(payload.ExtraAssigneeUserIds)      // + vài người cụ thể ngoài xưởng (nếu có)
                .Excluding(payload.CreatedByUserId),        // không tự báo cho người tạo
            employeesContent);

        // (2) Ví dụ nội dung KHÁC cho 1 nhóm khác (vd tổ trưởng/role quản lý) — tuỳ chọn.
        // var leaders = Plan(RecipientSpec.Roles(payload.LeaderRoleId), Content(...));
        // return Many(employees, leaders);

        return Task.FromResult<IReadOnlyList<NotificationPlanItem>>(new[] { employees });
    }
}

/// <summary>
/// Payload MẪU cho sự kiện tạo ca. Khi làm thật, thay bằng dữ liệu module Ca cung cấp
/// (thường là 1 record đặt cạnh nơi phát sự kiện).
/// </summary>
public sealed record ExampleShiftCreatedPayload(
    int ShiftId,
    int DepartmentId,
    DateTime WorkDate,
    int? CreatedByUserId,
    IReadOnlyCollection<int> ExtraAssigneeUserIds);
