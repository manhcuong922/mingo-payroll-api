using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// Lỗi #44 — <b>THỰC NHẬN không bao giờ được ÂM</b>.
///
/// <para><b>Nghiệp vụ</b> (`CAC-CASE-XAY-RA.md` §11.3, PO chốt 2026-07-23):
/// <i>"Tổng các khoản trừ lớn hơn thu nhập trong kỳ → Thực nhận <b>dừng ở 0, không bao giờ âm</b>;
/// phần trừ chưa thu hết được <b>chuyển sang kỳ lương kế tiếp</b> để tiếp tục thu, và chuỗi qua
/// nhiều kỳ vẫn đúng tổng."</i></para>
///
/// <para><b>Vì sao cần lớp này.</b> <see cref="PayrollAdjustmentApplier"/> đã chặn âm cho nhánh
/// TRUY THU, nhưng thực nhận còn có thể âm vì lý do khác — bất kỳ khoản trừ Input nào (cấn trừ tạm
/// ứng, chi khác...) hoặc thuế TNCN đủ lớn so với thu nhập kỳ đó đều có thể kéo công thức
/// <c>Cc.ThucNhan</c> xuống âm, vì công thức không tự kẹp 0. (Trước 2026-07-29, BHXH/BHYT/BHTN +
/// phí công đoàn tính trên <c>MUC_LUONG</c> nguyên trong khi Thu nhập chia theo công chuẩn cũng là
/// một nguồn gây âm — `[D20]`/PRORATE-03 đã sửa để BH chia theo công thực tế như Thu nhập, nhưng lớp
/// sàn 0 này vẫn cần cho MỌI nguồn âm khác, không riêng gì BH.) Xử lý ở đầu ra bằng sàn 0 + chuyển
/// dư là cách tổng quát, không cần biết trước nguyên nhân cụ thể.</para>
///
/// <para><b>Cách làm.</b> Kẹp ô THỰC NHẬN về 0 và ghi phần thiếu thành một khoản truy thu
/// <b>chưa áp</b> gắn với chính kỳ này. Kỳ lương kế tiếp sẽ tự gom nó qua
/// <see cref="PayrollAdjustmentApplier"/> — cơ chế đã có sẵn, nên chuỗi nhiều kỳ vẫn đúng tổng và
/// không cần đường xử lý riêng.</para>
/// </summary>
public static class PayrollNetFloor
{
    private const int ReasonMaxLength = 500;

    /// <summary>
    /// Áp sàn 0 cho THỰC NHẬN của <paramref name="row"/>.
    ///
    /// <returns>
    /// Khoản truy thu cần chuyển sang kỳ sau, hoặc <c>null</c> nếu thực nhận vốn đã ≥ 0.
    /// Caller chịu trách nhiệm gắn khoản này vào repository/DbContext.
    /// </returns>
    /// </summary>
    public static PayrollAdjustment? Apply(
        PayrollRow row,
        PayrollPeriod period,
        int userId,
        DateTime nowUtc)
    {
        var cell = row.Cells.FirstOrDefault(c => c.ColumnCode == PayrollColumnCodes.ThucNhan);
        if (cell is null) return null;

        // Kế toán ghim tay ô THỰC NHẬN thì tôn trọng ý chí người dùng — không tự kẹp.
        if (cell.IsOverridden) return null;

        var net = cell.ComputedValue ?? 0m;
        if (net >= 0m) return null;

        var shortfall = decimal.Round(-net, 2, MidpointRounding.AwayFromZero);
        cell.ComputedValue = 0m;

        var reason =
            $"Thực nhận kỳ {period.PeriodMonth:D2}/{period.PeriodYear} không đủ để trừ hết "
            + $"({shortfall:N0} đ còn thiếu) — chuyển sang kỳ sau thu tiếp.";

        return new PayrollAdjustment
        {
            EmployeeInformationId = row.EmployeeInformationId!.Value,
            SourcePeriod = period,
            AppliedPeriodId = null,          // chưa áp — kỳ kế tiếp sẽ gom
            Amount = -shortfall,             // âm = truy thu
            Reason = reason.Length <= ReasonMaxLength ? reason : reason[..ReasonMaxLength],
            CreatedAt = nowUtc,
            CreatedByUserId = userId,
        };
    }

    /// <summary>Chỉ áp cho dòng nhân viên — dòng TỔNG được tính lại từ các dòng đã kẹp.</summary>
    public static bool AppliesTo(PayrollRow row) =>
        row.RowType == PayrollRowType.Employee && row.EmployeeInformationId.HasValue;
}
