namespace MPS.Application.Features.Attendance.LeaveBalance.Common;

/// <summary>
/// BR-15 — công thức quỹ phép năm, dùng CHUNG cho cả 4 đường cấp quỹ (hook kích hoạt HĐ, cấp tay
/// qua API, job đầu năm, seeder) để không đường nào lệch đường nào.
///
/// <para><b>Công thức</b> (Đ.113 + Đ.114 BLLĐ 2019, Đ.66 NĐ 145/2020):</para>
/// <code>
/// Số ngày phép = (Số ngày nghỉ hằng năm + Số ngày tăng thêm theo thâm niên) / 12 × Số tháng làm việc
/// </code>
///
/// <list type="bullet">
///   <item>Số ngày nghỉ hằng năm = <see cref="BaseDaysPerYear"/> (12 ngày, Đ.113 k.1 điểm a).</item>
///   <item>Thâm niên: cứ đủ <see cref="SeniorityYearsPerBonusDay"/> (5) năm làm việc cho công ty thì
///     được thêm 01 ngày (Đ.114).</item>
///   <item>Số tháng làm việc trong năm — xem <see cref="MonthsWorkedInYear"/>. Làm đủ 12 tháng thì
///     hưởng nguyên quota, KHÔNG chia lại (tránh sai số làm tròn cho người làm cả năm).</item>
/// </list>
///
/// <para><b>Không cộng dồn năm cũ.</b> Quỹ năm N+1 tính lại từ đầu bằng chính công thức trên; phần
/// dư chưa dùng của năm N không được mang sang (<c>[D22]</c>).</para>
/// </summary>
public static class AnnualLeaveEntitlement
{
    /// <summary>Đ.113 k.1 điểm a — 12 ngày phép/năm cho điều kiện làm việc bình thường.</summary>
    public const decimal BaseDaysPerYear = 12m;

    /// <summary>Đ.114 — cứ đủ 5 năm làm việc cho một công ty thì được thêm 01 ngày.</summary>
    public const int SeniorityYearsPerBonusDay = 5;

    private const int MonthsPerYear = 12;

    /// <summary>
    /// Số ngày tăng thêm theo thâm niên tính tới 01/01 của <paramref name="year"/>.
    /// Chưa vào làm tính tới mốc đó ⇒ 0.
    /// </summary>
    public static int SeniorityBonusDays(DateTime joinDate, int year)
    {
        var anchor = new DateTime(year, 1, 1);
        if (anchor < joinDate.Date) return 0;

        var yearsOfService = (int)((anchor - joinDate.Date).TotalDays / 365);
        return yearsOfService / SeniorityYearsPerBonusDay;
    }

    /// <summary>
    /// Số tháng làm việc trong <paramref name="year"/>, suy từ khoảng phục vụ
    /// [<paramref name="joinDate"/> … <paramref name="serviceEnd"/>] cắt vào năm đó.
    /// <paramref name="serviceEnd"/> = null ⇒ HĐ không xác định thời hạn, phủ tới hết năm.
    ///
    /// <para>Tháng làm chưa trọn được tính là 01 tháng nếu phủ <b>từ 50%</b> số ngày của tháng
    /// (Đ.66 k.2 NĐ 145/2020). Ở đây đo bằng <b>ngày lịch được HĐ phủ</b> — giản lược so với luật
    /// (luật đếm ngày làm việc + ngày nghỉ hưởng lương) nhưng tất định và không cần chấm công.</para>
    /// </summary>
    public static int MonthsWorkedInYear(DateTime joinDate, DateTime? serviceEnd, int year)
    {
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);

        var from = joinDate.Date > yearStart ? joinDate.Date : yearStart;
        var to = serviceEnd.HasValue && serviceEnd.Value.Date < yearEnd ? serviceEnd.Value.Date : yearEnd;
        if (from > to) return 0;

        var months = 0;
        for (var month = 1; month <= MonthsPerYear; month++)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = new DateTime(year, month, daysInMonth);

            var coverFrom = from > monthStart ? from : monthStart;
            var coverTo = to < monthEnd ? to : monthEnd;
            if (coverFrom > coverTo) continue;

            var coveredDays = (coverTo - coverFrom).Days + 1;
            if (coveredDays * 2 >= daysInMonth) months++;
        }

        return months;
    }

    /// <summary>
    /// Chia quota theo số tháng làm việc. Đủ 12 tháng ⇒ trả nguyên <c>base + bonus</c>.
    /// Kết quả lẻ được làm tròn về hàng đơn vị, phần thập phân ≥ 0,5 làm tròn lên
    /// (Đ.66 k.3 NĐ 145/2020).
    /// </summary>
    public static decimal Prorate(decimal baseDays, decimal bonusDays, int monthsWorked)
    {
        if (monthsWorked >= MonthsPerYear) return baseDays + bonusDays;
        if (monthsWorked <= 0) return 0m;

        var raw = (baseDays + bonusDays) / MonthsPerYear * monthsWorked;
        return Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Quỹ phép NĂM của mã ANNUAL cho một người: gộp cả 3 bước (thâm niên → số tháng → chia).
    ///
    /// <para>Bảng <c>LeaveBalances</c> lưu tách <c>BaseDays</c> / <c>SeniorityBonusDays</c> nên kết
    /// quả đã chia cũng được tách lại theo đúng tỷ lệ đó, và <b>tổng luôn khớp</b>
    /// <c>TotalDays</c>: phần cơ bản chia riêng, phần thâm niên là chênh lệch còn lại. Người làm đủ
    /// 12 tháng ⇒ đúng <c>(12, thâm niên)</c> như trước.</para>
    /// </summary>
    public static AnnualLeaveGrant ForAnnual(DateTime joinDate, DateTime? serviceEnd, int year)
    {
        var fullBonus = (decimal)SeniorityBonusDays(joinDate, year);
        var monthsWorked = MonthsWorkedInYear(joinDate, serviceEnd, year);

        var totalDays = Prorate(BaseDaysPerYear, fullBonus, monthsWorked);
        var baseDays = Prorate(BaseDaysPerYear, 0m, monthsWorked);

        return new AnnualLeaveGrant(baseDays, totalDays - baseDays, monthsWorked, totalDays);
    }
}

/// <summary>Kết quả tính quỹ phép năm — tách sẵn từng thành phần để ghi vào bảng và tra soát.</summary>
public sealed record AnnualLeaveGrant(
    decimal BaseDays,
    decimal SeniorityBonusDays,
    int MonthsWorked,
    decimal TotalDays);
