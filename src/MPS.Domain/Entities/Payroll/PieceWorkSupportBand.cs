using MPS.Domain.Attributes;

namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// Bậc thang khoản <b>hỗ trợ theo tiến độ</b> của HỢP ĐỒNG KHOÁN VIỆC: tổng giờ làm trong tháng
/// rơi vào khoảng [MinHours, MaxHours] → trả <see cref="Amount"/>. Cấu hình được (quyết định PO
/// 18/08/2026: từ 150h = 200.000đ, từ 200h = 500.000đ).
///
/// <para><b>Không cộng dồn</b>: chọn ĐÚNG MỘT bậc — bậc có <see cref="MinHours"/> lớn nhất mà tổng
/// giờ vẫn thoả. Người làm 210h nhận 500.000đ, không phải 700.000đ.</para>
///
/// <para><see cref="MaxHours"/> null = bậc trên cùng, không có trần.</para>
///
/// <para>Chỉ áp cho <c>ContractKind.PieceWork</c>; mọi loại HĐ khác giữ nguyên cột "Trợ cấp lương
/// chính" = mức lương × hệ số năng suất (xem <c>ProductivityCoefficient</c>).</para>
///
/// <para>⚠️ Nhãn hiển thị trên bảng lương / phiếu lương / Excel phải là "Hỗ trợ theo tiến độ",
/// KHÔNG dùng chữ "lương" hay "thưởng" — chứng từ của hợp đồng dân sự không được mang ngôn ngữ của
/// quan hệ lao động. Xem docs/plans/piecework-contract-and-midperiod-payroll-plan.md §0.3.</para>
/// </summary>
[Auditable]
public class PieceWorkSupportBand
{
    public int Id { get; set; }

    /// <summary>Ngưỡng giờ tối thiểu (bao gồm) để hưởng bậc này.</summary>
    public decimal MinHours { get; set; }

    /// <summary>Ngưỡng giờ tối đa (bao gồm); null = không có trần.</summary>
    public decimal? MaxHours { get; set; }

    /// <summary>Số tiền hỗ trợ của bậc.</summary>
    public decimal Amount { get; set; }

    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
