namespace MPS.Domain.Entities.Compliance;

public class OtRateEntry
{
    public int Id { get; set; }
    public int OtRateVersionId { get; set; }
    public OtRateVersion OtRateVersion { get; set; } = null!;

    /// <summary>
    /// Loại OT, vd "Weekday", "Weekend", "Holiday", "NightShift".
    /// Lưu string (max 50) — không dùng enum để không buộc redeploy khi luật thêm loại OT.
    /// Tham chiếu hằng số tại <see cref="MPS.Domain.Enums.Compliance.OtRateTypes"/>.
    /// </summary>
    public string OtType { get; set; } = string.Empty;

    /// <summary>Nhãn hiển thị cho HR, vd "Ngày thường", "Cuối tuần".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Hệ số nhân lương, vd 1.50, 2.00, 3.00, 1.30.
    /// Validator kiểm tra từng loại có floor riêng (Weekday ≥ 1.50, ...).
    /// </summary>
    public decimal Rate { get; set; }
}
