using FluentAssertions;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Attendance;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// #59 (HOL-STD01) — công chuẩn mặc định phải TRỪ ngày lễ. CONGCHUAN là mẫu số của mọi phép chia
/// lương ⇒ đặt cao hơn thực tế thì mỗi ngày công của NLĐ bị quy đổi rẻ đi.
/// </summary>
public sealed class PayrollWorkingDaysTests
{
    [Theory]
    [InlineData(2026, 1)]  // Tết Dương lịch 01/01 (thứ Năm)
    [InlineData(2026, 4)]  // Giải phóng 30/04 (thứ Năm)
    [InlineData(2026, 5)]  // Quốc tế LĐ 01/05 (thứ Sáu)
    [InlineData(2026, 9)]  // Quốc khánh 02/09 (thứ Tư)
    public void CountStandardWorkingDays_Subtracts_Statutory_Solar_Holiday_On_Weekday(int year, int month)
    {
        var weekdays = PayrollWorkingDays.CountWeekdays(year, month);
        var standard = PayrollWorkingDays.CountStandardWorkingDays(year, month);

        standard.Should().Be(weekdays - 1, "tháng có đúng 1 ngày lễ dương lịch cố định rơi vào T2–T6");
    }

    [Fact]
    public void CountStandardWorkingDays_Month_Without_Statutory_Holiday_Equals_Weekdays()
    {
        // Tháng 6 không có mốc lễ dương lịch cố định nào.
        PayrollWorkingDays.CountStandardWorkingDays(2026, 6)
            .Should().Be(PayrollWorkingDays.CountWeekdays(2026, 6));
    }

    [Fact]
    public void CountStandardWorkingDays_Subtracts_Entered_Holiday_On_Weekday()
    {
        var entered = new[] { new DateOnly(2026, 6, 15) }; // thứ Hai
        PayrollWorkingDays.CountStandardWorkingDays(2026, 6, entered)
            .Should().Be(PayrollWorkingDays.CountWeekdays(2026, 6) - 1);
    }

    [Fact]
    public void CountStandardWorkingDays_Ignores_Entered_Holiday_On_Weekend()
    {
        var entered = new[] { new DateOnly(2026, 6, 13) }; // thứ Bảy — không phải ngày công
        PayrollWorkingDays.CountStandardWorkingDays(2026, 6, entered)
            .Should().Be(PayrollWorkingDays.CountWeekdays(2026, 6));
    }

    [Fact]
    public void CountStandardWorkingDays_Does_Not_Double_Count_Statutory_Also_Entered()
    {
        // Nhập lại đúng ngày lễ luật (01/05) không được trừ 2 lần.
        var entered = new[] { new DateOnly(2026, 5, 1) };
        PayrollWorkingDays.CountStandardWorkingDays(2026, 5, entered)
            .Should().Be(PayrollWorkingDays.CountWeekdays(2026, 5) - 1);
    }

    [Fact]
    public void HolidayDatesInMonth_Expands_Range_And_Clips_To_Month()
    {
        // Lễ vắt ngày 29/04 → 02/05: tháng 4 chỉ lấy {29, 30}.
        var holiday = new PublicHoliday
        {
            HolidayDate = new DateOnly(2026, 4, 29),
            HolidayEndDate = new DateOnly(2026, 5, 2),
            IsActive = true,
        };

        var april = PayrollWorkingDays.HolidayDatesInMonth(new[] { holiday }, 2026, 4);
        april.Should().BeEquivalentTo(new[] { new DateOnly(2026, 4, 29), new DateOnly(2026, 4, 30) });

        var may = PayrollWorkingDays.HolidayDatesInMonth(new[] { holiday }, 2026, 5);
        may.Should().BeEquivalentTo(new[] { new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 2) });
    }

    [Fact]
    public void HolidayDatesInMonth_Single_Day_Holiday_Is_Included()
    {
        var holiday = new PublicHoliday
        {
            HolidayDate = new DateOnly(2026, 9, 2),
            HolidayEndDate = new DateOnly(2026, 9, 2),
            IsActive = true,
        };
        PayrollWorkingDays.HolidayDatesInMonth(new[] { holiday }, 2026, 9)
            .Should().ContainSingle().Which.Should().Be(new DateOnly(2026, 9, 2));
    }
}
