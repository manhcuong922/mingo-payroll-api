using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// Chọn số tiền <b>hỗ trợ theo tiến độ</b> của HĐ khoán việc từ tổng giờ làm trong tháng.
///
/// <para>Quyết định PO 18/08/2026: từ 150h = 200.000đ, từ 200h = 500.000đ, <b>không cộng dồn</b> —
/// bậc cao thay thế bậc thấp chứ không cộng thêm. Bậc lưu ở <see cref="PieceWorkSupportBand"/> nên
/// đổi mốc / số tiền không cần deploy.</para>
///
/// <para>Tách thành hàm thuần (không I/O) để cùng một logic dùng được cho collector, cho việc tính
/// lại kỳ cũ từ snapshot, và test được theo từng biên.</para>
/// </summary>
public static class PieceWorkSupportResolver
{
    /// <summary>
    /// Tiền hỗ trợ ứng với <paramref name="totalHours"/>; 0 nếu không bậc nào khớp.
    ///
    /// <para>Khi nhiều bậc cùng khớp (biên chồng lấn, vd đúng 200h khớp cả [150,200] lẫn [200,∞)),
    /// lấy bậc có <see cref="PieceWorkSupportBand.MinHours"/> LỚN NHẤT — bậc cao thắng.</para>
    /// </summary>
    public static decimal Resolve(IReadOnlyList<PieceWorkSupportBand> bands, decimal totalHours)
    {
        if (bands.Count == 0 || totalHours <= 0m) return 0m;

        var matched = bands
            .Where(b => b.IsActive
                && totalHours >= b.MinHours
                && (b.MaxHours is not { } max || totalHours <= max))
            .OrderByDescending(b => b.MinHours)
            .FirstOrDefault();

        return matched?.Amount ?? 0m;
    }
}
