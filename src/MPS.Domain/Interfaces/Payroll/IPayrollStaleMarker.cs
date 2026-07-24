namespace MPS.Domain.Interfaces.Payroll;

/// <summary>
/// BS-6/[D1] — đánh cờ "cần tính lại" cho các kỳ lương Draft khi một NGUỒN của kỳ đổi.
/// Chỉ chạm kỳ Draft (Finalized đã đóng băng). Best-effort: đây là side-effect PHỤ của thao tác
/// nguồn — người gọi nên nuốt lỗi để không làm hỏng nghiệp vụ chính (duyệt đơn, sửa công…).
/// </summary>
public interface IPayrollStaleMarker
{
    /// <summary>Đánh cờ kỳ Draft đúng (năm, tháng). Không có kỳ nào khớp thì bỏ qua.</summary>
    Task MarkStaleForMonthAsync(int year, int month, string reason, CancellationToken ct = default);

    /// <summary>Đánh cờ mọi kỳ Draft mà (năm, tháng) rơi vào tập tháng bị ảnh hưởng (đơn vắt nhiều tháng).</summary>
    Task MarkStaleForMonthsAsync(IEnumerable<(int Year, int Month)> months, string reason, CancellationToken ct = default);

    /// <summary>
    /// Đánh cờ mọi kỳ Draft từ (năm, tháng) trở về SAU — dùng cho phụ lục HĐ đổi lương/phụ cấp,
    /// vì thay đổi có hiệu lực kéo dài sang các kỳ kế tiếp chưa chốt.
    /// </summary>
    Task MarkStaleFromMonthAsync(int year, int month, string reason, CancellationToken ct = default);
}
