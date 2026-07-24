namespace MPS.Domain.Interfaces.Payroll;

/// <summary>
/// Cổng duy nhất mà các handler nguồn (duyệt OT/nghỉ/WFH/đi-muộn/điều chỉnh công, ký phụ lục, duyệt
/// đánh giá tháng…) gọi khi số liệu đầu vào của một tháng lương đổi.
///
/// Service tự phân nhánh theo trạng thái kỳ:
/// <list type="bullet">
///   <item>Kỳ <b>Draft</b> ⇒ mark stale (BS-6) qua <see cref="IPayrollStaleMarker"/>.</item>
///   <item>Kỳ <b>Finalized</b> ⇒ capture chênh lệch THUCNHAN của NV (BS-5) qua
///         <see cref="IPayrollAdjustmentCapture"/>.</item>
/// </list>
///
/// Best-effort: là side-effect PHỤ của thao tác nguồn — lỗi được nuốt + log tại chỗ. Worst case:
/// kỳ không được đánh cờ / adjustment không được sinh — HR vẫn có đường tay để tính lại/tạo bù.
/// </summary>
public interface IPayrollSourceChangeHandler
{
    /// <summary>
    /// Nguồn của <paramref name="months"/> đổi cho các NV trong <paramref name="employeeInformationIds"/>.
    /// - Draft: mark toàn kỳ (không cần empIds), dùng <see cref="IPayrollStaleMarker"/>.
    /// - Finalized: capture cho từng NV trong empIds (nếu empIds rỗng: bỏ qua nhánh Finalized).
    /// </summary>
    Task OnSourceChangedAsync(
        IEnumerable<(int Year, int Month)> months,
        IReadOnlyCollection<int> employeeInformationIds,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// Đơn giản: 1 tháng, 1 NV — tương đương gọi <see cref="OnSourceChangedAsync"/> với danh sách 1
    /// phần tử. Đường tắt cho handler làm việc trên 1 request/1 record.
    /// </summary>
    Task OnSourceChangedAsync(
        int year, int month, int employeeInformationId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Dùng cho phụ lục HĐ: mọi kỳ từ tháng (<paramref name="year"/>, <paramref name="month"/>) trở về SAU.
    /// - Draft: mark stale (kỳ chưa chốt).
    /// - Finalized: KHÔNG capture retroactively (phụ lục có hiệu lực từ ngày ký trở về sau; kỳ đã
    ///   chốt trước ngày hiệu lực đúng theo mức lương lúc chốt). Chỉ ảnh hưởng kỳ chưa chốt.
    /// </summary>
    Task OnEffectiveFromChangedAsync(
        int year, int month, int employeeInformationId, string reason, CancellationToken ct = default);
}
