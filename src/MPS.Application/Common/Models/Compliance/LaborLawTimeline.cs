namespace MPS.Application.Common.Models.Compliance;

/// <summary>
/// ⭐ [Mục 1 — luật theo từng ngày] Bảng tra hằng số luật lao động THEO NGÀY của một kỳ lương.
///
/// <para>Trước đây payroll lấy MỘT snapshot tại <c>PayDate</c> (ngày cuối tháng) rồi áp cho cả 30 ngày,
/// nên luật đổi giữa tháng bị áp HỒI TỐ cho những ngày trước ngày hiệu lực. Các khoản gắn với một
/// NGÀY LÀM VIỆC cụ thể — hệ số tăng ca, mức trả ngày nghỉ tuần/lễ, phụ cấp làm đêm, khung giờ đêm —
/// phải tra theo đúng ngày đó.</para>
///
/// <para><b>Ranh giới cố ý:</b> chỉ những đại lượng "theo ngày" mới dùng bảng này. Các đại lượng bản
/// chất là "cả kỳ" (đơn giá giờ = mức lương / (công chuẩn × giờ chuẩn/ngày), công chuẩn/tháng) vẫn
/// dùng <see cref="PeriodEnd"/> vì chúng là MỘT con số cho cả bảng lương, không chia theo ngày được.</para>
/// </summary>
public sealed class LaborLawTimeline
{
    private readonly IReadOnlyDictionary<DateOnly, LaborLawConstantsSnapshot> _byDate;

    public LaborLawTimeline(
        IReadOnlyDictionary<DateOnly, LaborLawConstantsSnapshot> byDate,
        LaborLawConstantsSnapshot periodEnd)
    {
        _byDate = byDate;
        PeriodEnd = periodEnd;
        Versions = byDate.Values
            .Append(periodEnd)
            .GroupBy(v => v.VersionId)
            .Select(g => g.First())
            .OrderBy(v => v.ValidFrom)
            .ToList();
    }

    /// <summary>
    /// Snapshot tại NGÀY CUỐI KỲ — mốc dùng cho các con số bản chất là "cả kỳ", và là giá trị dự phòng
    /// cho ngày nằm ngoài bảng.
    /// </summary>
    public LaborLawConstantsSnapshot PeriodEnd { get; }

    /// <summary>Các version luật lao động đã dùng trong kỳ, xếp theo ngày hiệu lực tăng dần.</summary>
    public IReadOnlyList<LaborLawConstantsSnapshot> Versions { get; }

    /// <summary>Có từ 2 version trở lên ⇒ luật đã đổi GIỮA KỲ, kỳ lương cần ghi chú cho kế toán.</summary>
    public bool ChangedWithinPeriod => Versions.Count > 1;

    /// <summary>
    /// Hằng số luật áp cho <paramref name="date"/>. Ngày ngoài bảng (dữ liệu lệch kỳ) rơi về
    /// <see cref="PeriodEnd"/> thay vì ném lỗi — bảng lương không được vỡ vì một bản ghi lạc ngày.
    /// </summary>
    public LaborLawConstantsSnapshot For(DateOnly date) =>
        _byDate.TryGetValue(date, out var snapshot) ? snapshot : PeriodEnd;

    /// <summary>
    /// Timeline một mức duy nhất cho mọi ngày — dùng cho đường không có đổi luật trong kỳ và cho test
    /// phần số học. Giữ nguyên đúng hành vi cũ (một snapshot cho cả kỳ).
    /// </summary>
    public static LaborLawTimeline Uniform(LaborLawConstantsSnapshot snapshot) =>
        new(new Dictionary<DateOnly, LaborLawConstantsSnapshot>(), snapshot);
}
