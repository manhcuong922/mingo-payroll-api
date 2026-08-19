using FluentAssertions;
using MPS.Application.Features.Payroll.Common;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// ⭐ CC-2026-08 — công chuẩn = số ngày T2–T6 của tháng, <b>KHÔNG trừ ngày lễ</b>
/// (đảo lại quy tắc #59/HOL-STD01 cũ).
///
/// <para>Ngày lễ không biến mất khỏi phép tính, nó chuyển sang TỬ SỐ: mỗi ngày lễ T2–T6 được cộng
/// thành 1 công hưởng lương (Đ.112 BLLĐ) — xem
/// <c>MPS.Infrastructure.Tests.Services.PayrollCollectorHolidayCreditTests</c>. Vế mẫu số ở tầng
/// tạo kỳ do <c>CreatePayrollPeriodStandardDaysTests</c> ghim.</para>
///
/// <para>File này chỉ còn kiểm phép đếm nền: đúng số ngày T2–T6, và trơ với ngày lễ.</para>
/// </summary>
public sealed class PayrollWorkingDaysTests
{
    [Theory(DisplayName = "Đếm đúng số ngày T2–T6 của tháng")]
    [InlineData(2026, 1, 22)]
    [InlineData(2026, 2, 20)]
    [InlineData(2026, 5, 21)]
    [InlineData(2026, 6, 22)]
    [InlineData(2026, 9, 22)]
    public void CountWeekdays_Returns_Number_Of_Weekdays(int year, int month, int expected)
        => PayrollWorkingDays.CountWeekdays(year, month).Should().Be(expected);

    [Theory(DisplayName = "Ngày lễ dương lịch cố định KHÔNG bị trừ khỏi công chuẩn")]
    [InlineData(2026, 1)]  // Tết Dương lịch 01/01 — Thứ Năm
    [InlineData(2026, 4)]  // Giải phóng 30/04 — Thứ Năm
    [InlineData(2026, 5)]  // Quốc tế Lao động 01/05 — Thứ Sáu
    [InlineData(2026, 9)]  // Quốc khánh 02/09 — Thứ Tư
    public void CountWeekdays_Ignores_Statutory_Holidays(int year, int month)
    {
        var totalDays = DateTime.DaysInMonth(year, month);
        var weekdays = Enumerable.Range(1, totalDays)
            .Select(d => new DateOnly(year, month, d))
            .Count(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));

        PayrollWorkingDays.CountWeekdays(year, month).Should().Be(weekdays,
            "mẫu số không được co lại vì tháng có lễ — nếu không, nghỉ 1 ngày trong tháng Tết "
            + "bị trừ lương nặng hơn hẳn tháng thường");
    }

    [Fact(DisplayName = "Tháng 2 năm nhuận vẫn đếm đúng")]
    public void CountWeekdays_Handles_Leap_February()
        => PayrollWorkingDays.CountWeekdays(2028, 2).Should().Be(21);
}
