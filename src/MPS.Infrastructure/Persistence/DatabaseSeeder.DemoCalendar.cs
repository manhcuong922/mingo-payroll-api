using Microsoft.Extensions.Configuration;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Trục thời gian của dữ liệu demo — NEO CỨNG thay vì trôi theo <c>DateTime.UtcNow</c>.
///
/// <para><b>Vì sao neo cứng.</b> Các mốc PHÁP LUẬT là ngày cố định: 01/07/2026 đổi đồng thời mức
/// tham chiếu BHXH, biểu thuế TNCN + ngưỡng thuế khoán 5tr (NĐ 253/2026), trần phụ cấp ăn ca
/// 730k → 1,2tr, và miễn thuế toàn bộ tiền tăng ca. Cặp kỳ <b>06/2026 (luật cũ) ↔ 07/2026 (luật
/// mới)</b> vì thế là chỗ duy nhất quan sát được các nhánh đó bằng dữ liệu thật — mà cửa sổ "3 kỳ
/// gần nhất" tính theo ngày chạy sẽ trượt khỏi cặp này chỉ sau vài tháng.</para>
///
/// <para>Neo cứng còn cho phép ghi SỐ LIỆU KỲ VỌNG vào tài liệu và đối chiếu tay — thứ không làm
/// được khi mỗi lần dựng DB lại ra một khoảng thời gian khác.</para>
///
/// <para>Đổi trục qua config <c>Seed:DemoAnchorMonth</c> (dạng <c>yyyy-MM</c>).</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    /// <summary>Kỳ lương DEMO cuối cùng được sinh. Ba kỳ demo = tháng này và 2 tháng liền trước.</summary>
    private const string DefaultDemoAnchorMonth = "2026-07";

    private DateOnly? _demoAnchorCache;

    /// <summary>Ngày đầu của kỳ lương demo MỚI NHẤT (mặc định 01/07/2026).</summary>
    private DateOnly DemoAnchorMonth
    {
        get
        {
            if (_demoAnchorCache is DateOnly cached) return cached;

            var raw = _configuration["Seed:DemoAnchorMonth"] ?? DefaultDemoAnchorMonth;
            var parsed = DateOnly.TryParseExact(raw, "yyyy-MM", null,
                System.Globalization.DateTimeStyles.None, out var value)
                ? value
                : DateOnly.ParseExact(DefaultDemoAnchorMonth, "yyyy-MM");

            _demoAnchorCache = parsed;
            return parsed;
        }
    }

    /// <summary>
    /// Tháng "hiện tại" của thế giới demo = ngay sau kỳ lương cuối cùng. Đây là chỗ đặt các case cố ý
    /// làm ĐỎ cổng đủ liệu: cổng chặn CẢ KỲ nên case đỏ nằm trong 3 kỳ demo sẽ khiến không sinh được
    /// kỳ nào.
    /// </summary>
    private DateOnly DemoCurrentMonth => DemoAnchorMonth.AddMonths(1);

    /// <summary>Ngày đầu của kỳ demo thứ <paramref name="index"/> tính từ cũ nhất (0..2).</summary>
    private DateOnly DemoPeriodStart(int index) => DemoAnchorMonth.AddMonths(index - (PayrollDemoMonths - 1));

    /// <summary>Ngày cụ thể trong tháng của một kỳ demo — dùng cho các mốc hiệu lực giữa kỳ.</summary>
    private DateOnly DemoDay(int periodIndex, int day) => DemoPeriodStart(periodIndex).AddDays(day - 1);

    /// <summary>Mốc UTC 00:00 của một ngày — khớp quy ước ghi <c>EffectiveFrom</c>/<c>EffectiveTo</c>.</summary>
    private static DateTime Utc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
