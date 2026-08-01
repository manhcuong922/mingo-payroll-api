using FluentAssertions;
using MPS.Application.Features.Attendance.Attendance.Commands;
using MPS.Application.Features.Attendance.LeaveRequest.Commands;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Application.Features.Attendance.WfhRequest.Commands;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// B1 — Phạm vi của cơ chế "kỳ lương cần tính lại" (<see cref="IPayrollSourceChangeHandler"/>).
///
/// <para><b>PO đã chốt (2026-08-01): GIỮ NGUYÊN phạm vi hiện tại.</b> Cơ chế chỉ gắn vào các
/// handler DUYỆT đơn + kích hoạt HĐ/phụ lục + duyệt/mở lại đánh giá tháng. Các đường ghi chấm công
/// trực tiếp và các đường huỷ đơn đã duyệt <b>cố ý</b> không bật cờ.</para>
///
/// <para>Đây là <b>test ghim quyết định</b> (characterization), không phải test lỗi: nó mô tả phạm
/// vi đã chốt để lần sau ai thấy "sửa chấm công mà kỳ lương không stale" thì đây là chỗ tra, không
/// phải bug mới. Test đỏ = phạm vi vừa bị đổi ngoài ý muốn ⇒ phải quay lại đọc quyết định trước.</para>
///
/// <para>⚠️ <b>Hệ quả đã được chấp nhận</b> — cần bù bằng quy trình vận hành, không bằng code:</para>
/// <list type="bullet">
///   <item>HR sửa chấm công ⇒ kỳ Draft KHÔNG tự bật cờ "cần tính lại"; kế toán mở bảng lương thấy
///   trạng thái "sạch". Bù: kế toán phải <b>luôn bấm "Tính lại kỳ"</b> ngay trước khi Chốt.</item>
///   <item>Với kỳ ĐÃ CHỐT, thay đổi chấm công KHÔNG sinh khoản truy lĩnh/truy thu tự động.
///   Bù: dùng màn hình khoản điều chỉnh (<c>CreatePayrollPeriodAdjustment</c>) để nhập tay.</item>
/// </list>
/// </summary>
public sealed class PayrollStaleSourceCoverageTests
{
    private static bool DependsOnPayrollSourceChange(Type handlerType) =>
        handlerType.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(IPayrollSourceChangeHandler));

    /// <summary>7 handler nằm NGOÀI phạm vi cơ chế stale theo quyết định đã chốt.</summary>
    public static TheoryData<Type, string> DeliberatelyOutsideStaleMechanism() => new()
    {
        { typeof(RecordAttendanceCommandHandler), "HR chấm công trực tiếp cho NV" },
        { typeof(BulkRecordAttendanceCommandHandler), "HR chấm công hàng loạt" },
        { typeof(UpdateAttendanceCommandHandler), "HR sửa giờ vào/ra của một ngày" },
        { typeof(UpdateAttendanceByIdCommandHandler), "HR sửa bản ghi chấm công theo Id" },
        { typeof(CancelLeaveRequestCommandHandler), "NLĐ huỷ đơn nghỉ phép đã duyệt" },
        { typeof(CancelOvertimeRequestCommandHandler), "NLĐ huỷ đơn OT đã duyệt" },
        { typeof(CancelWfhRequestCommandHandler), "NLĐ huỷ đơn WFH đã duyệt" },
    };

    [Theory(DisplayName = "B1 · [Quyết định] Thao tác NGOÀI phạm vi cơ chế stale")]
    [MemberData(nameof(DeliberatelyOutsideStaleMechanism))]
    public void B1_TheseOperations_DeliberatelyDoNotMarkPayrollStale(Type handlerType, string scenario)
    {
        DependsOnPayrollSourceChange(handlerType).Should().BeFalse(
            "{0} — PO chốt giữ ngoài phạm vi cơ chế stale; bù bằng thao tác 'Tính lại kỳ' trước khi " +
            "Chốt (kỳ Draft) và khoản điều chỉnh nhập tay (kỳ đã chốt). Nếu test này ĐỎ nghĩa là ai " +
            "đó vừa mở rộng phạm vi — hãy xác nhận lại với PO trước khi giữ thay đổi", scenario);
    }

    /// <summary>Các handler NẰM TRONG phạm vi — ghim để biên giới quyết định có hai phía rõ ràng.</summary>
    public static TheoryData<Type, string> InsideStaleMechanism() => new()
    {
        { typeof(ApproveLeaveRequestCommandHandler), "Duyệt đơn nghỉ phép" },
        { typeof(ApproveOvertimeRequestCommandHandler), "Duyệt đơn OT" },
        { typeof(ApproveWfhRequestCommandHandler), "Duyệt đơn WFH" },
    };

    [Theory(DisplayName = "B1 · [Quyết định] Thao tác TRONG phạm vi cơ chế stale")]
    [MemberData(nameof(InsideStaleMechanism))]
    public void B1_ApproveHandlers_DoMarkPayrollStale(Type handlerType, string scenario)
    {
        DependsOnPayrollSourceChange(handlerType).Should().BeTrue(
            "{0} — nhánh duyệt đơn LÀ phần được cơ chế stale bao phủ; mất kết nối này là hồi quy thật",
            scenario);
    }
}
