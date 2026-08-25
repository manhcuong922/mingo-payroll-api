using FluentAssertions;
using MPS.Application.Features.Attendance.Attendance.Commands;
using MPS.Application.Features.Attendance.AttendanceSheet.Commands;
using MPS.Application.Features.Attendance.FaceRegistration.Commands;
using MPS.Application.Features.Attendance.LeaveRequest.Commands;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Application.Features.Attendance.WfhRequest.Commands;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// B1 — Phạm vi của cơ chế "kỳ lương cần tính lại" (<see cref="IPayrollSourceChangeHandler"/>).
///
/// <para><b>Lịch sử quyết định.</b> PO chốt 2026-08-01: giữ các đường GHI CHẤM CÔNG trực tiếp nằm
/// ngoài phạm vi, bù bằng quy trình vận hành ("kế toán luôn bấm Tính lại trước khi Chốt").
/// <b>Đảo lại 2026-08-24</b> cho riêng nhóm chấm công, vì phần "bù bằng quy trình" không đứng được
/// trước một ca có thật:</para>
///
/// <list type="bullet">
///   <item>Tiền OT chỉ phát sinh khi CÓ bản ghi chấm công gắn <c>OvertimeRequestId</c>
///   (<c>PayrollDataCollector.ComputeOvertimePayAsync</c> — đơn duyệt suông = 0đ), mà bản ghi đó
///   LUÔN tới SAU khi duyệt đơn (chấm công OT đòi đơn đã Approved).</item>
///   <item>Cổng capture khoản truy lĩnh cho kỳ ĐÃ CHỐT lại chỉ gắn ở bước DUYỆT ĐƠN ⇒ lúc nó chạy
///   OT vẫn 0 giờ ⇒ chênh lệch = 0 ⇒ <b>không khoản truy lĩnh nào được sinh</b>. OT hồi tố (điển
///   hình: OT ngày cuối tháng, duyệt sau khi kỳ đã chốt) mất tiền âm thầm, và không có thao tác tay
///   nào chạy lại được capture — quy trình vận hành không cứu được.</item>
/// </list>
///
/// <para>Nhóm HUỶ đơn đã duyệt vẫn <b>giữ nguyên</b> ngoài phạm vi theo quyết định 2026-08-01.</para>
///
/// <para>Đây là <b>test ghim quyết định</b> (characterization), không phải test lỗi: nó mô tả biên
/// giới đã chốt để lần sau ai thấy lệch thì đây là chỗ tra. Test đỏ = phạm vi vừa bị đổi ngoài ý
/// muốn ⇒ phải quay lại đọc quyết định trước.</para>
/// </summary>
public sealed class PayrollStaleSourceCoverageTests
{
    private static bool DependsOnPayrollSourceChange(Type handlerType) =>
        handlerType.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(IPayrollSourceChangeHandler));

    /// <summary>3 handler còn nằm NGOÀI phạm vi cơ chế stale theo quyết định 2026-08-01.</summary>
    public static TheoryData<Type, string> DeliberatelyOutsideStaleMechanism() => new()
    {
        { typeof(CancelLeaveRequestCommandHandler), "NLĐ huỷ đơn nghỉ phép đã duyệt" },
        { typeof(CancelOvertimeRequestCommandHandler), "NLĐ huỷ đơn OT đã duyệt" },
        { typeof(CancelWfhRequestCommandHandler), "NLĐ huỷ đơn WFH đã duyệt" },
    };

    [Theory(DisplayName = "B1 · [Quyết định] Thao tác NGOÀI phạm vi cơ chế stale")]
    [MemberData(nameof(DeliberatelyOutsideStaleMechanism))]
    public void B1_TheseOperations_DeliberatelyDoNotMarkPayrollStale(Type handlerType, string scenario)
    {
        DependsOnPayrollSourceChange(handlerType).Should().BeFalse(
            "{0} — PO chốt (2026-08-01) giữ nhánh HUỶ đơn ngoài phạm vi cơ chế stale. Nếu test này " +
            "ĐỎ nghĩa là ai đó vừa mở rộng phạm vi — hãy xác nhận lại với PO trước khi giữ thay đổi",
            scenario);
    }

    /// <summary>Các handler NẰM TRONG phạm vi — ghim để biên giới quyết định có hai phía rõ ràng.</summary>
    public static TheoryData<Type, string> InsideStaleMechanism() => new()
    {
        { typeof(ApproveLeaveRequestCommandHandler), "Duyệt đơn nghỉ phép" },
        { typeof(ApproveOvertimeRequestCommandHandler), "Duyệt đơn OT" },
        { typeof(ApproveWfhRequestCommandHandler), "Duyệt đơn WFH" },
        // Nhóm GHI CHẤM CÔNG — vào phạm vi từ 2026-08-24. Chấm công OT là chỗ DUY NHẤT biết được
        // "tiền tăng ca vừa thật sự phát sinh"; mất kết nối ở đây là OT hồi tố mất truy lĩnh.
        { typeof(RecordAttendanceCommandHandler), "HR chấm công trực tiếp cho NV (gồm chấm công OT)" },
        { typeof(BulkRecordAttendanceCommandHandler), "HR chấm công hàng loạt" },
        { typeof(UpdateAttendanceCommandHandler), "HR sửa giờ vào/ra của một ngày" },
        { typeof(UpdateAttendanceByIdCommandHandler), "HR sửa bản ghi chấm công theo Id" },
        { typeof(ConfirmAttendanceSheetImportCommandHandler), "HR import bảng chấm công Excel" },
        { typeof(FaceCheckinCommandHandler), "NLĐ quét mặt vào/tan ca (sinh cả bản ghi OT thực tế)" },
    };

    [Theory(DisplayName = "B1 · [Quyết định] Thao tác TRONG phạm vi cơ chế stale")]
    [MemberData(nameof(InsideStaleMechanism))]
    public void B1_SourceHandlers_DoMarkPayrollStale(Type handlerType, string scenario)
    {
        DependsOnPayrollSourceChange(handlerType).Should().BeTrue(
            "{0} — nhánh này LÀ phần được cơ chế stale bao phủ; mất kết nối này là hồi quy thật",
            scenario);
    }
}
