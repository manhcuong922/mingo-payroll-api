namespace MPS.Domain.Interfaces.Payroll;

/// <summary>
/// BS-5 — khi NGUỒN của kỳ Finalized đổi, capture chênh lệch THUCNHAN của NV bị ảnh hưởng
/// thành một khoản <c>PayrollAdjustment</c> (chưa áp). Kỳ mới sẽ gom các khoản chưa áp vào cột
/// TRUY_LINH / TRUY_THU (phương án A đã chốt với PO 2026-07-21 — không tính thuế lại).
///
/// Chỉ chạm kỳ Finalized (Draft có <c>IPayrollStaleMarker</c> đánh cờ để tính lại nguyên kỳ).
/// Best-effort: là side-effect PHỤ của thao tác sửa nguồn — người gọi nuốt lỗi để không làm hỏng
/// nghiệp vụ chính. Worst case: kỳ mới không nhận truy lĩnh → HR có thể tạo bù tay.
/// </summary>
public interface IPayrollAdjustmentCapture
{
    /// <summary>
    /// Với từng NV thuộc <paramref name="employeeInformationIds"/>: nếu <paramref name="sourcePeriodId"/>
    /// đang ở <c>Finalized</c>, tính lại THUCNHAN của dòng NV bằng NGUỒN MỚI + công thức đã đóng băng
    /// của kỳ (bao gồm áp lại các ô ghim cũ), so với THUCNHAN cũ để lấy diff. Upsert khoản chênh lệch
    /// vào <c>PayrollAdjustments</c> (khoản có <c>AppliedPeriodId = null</c> cho cùng cặp
    /// <c>(SourcePeriodId, EmployeeInformationId)</c> sẽ được cộng dồn ghi đè).
    /// </summary>
    Task CaptureForFinalizedPeriodAsync(
        int sourcePeriodId,
        IReadOnlyCollection<int> employeeInformationIds,
        string reason,
        CancellationToken ct = default);
}
