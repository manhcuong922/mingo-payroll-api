using FluentAssertions;
using MPS.Application.Features.Attendance.LeaveBalance.Common;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// [D22] — công thức quỹ phép năm:
/// <c>(số ngày nghỉ hằng năm + số ngày tăng thêm theo thâm niên) / 12 × số tháng làm việc</c>.
/// Thâm niên: cứ đủ 05 năm làm việc cho công ty được thêm 01 ngày (Đ.114).
/// </summary>
public sealed class AnnualLeaveEntitlementTests
{
    // ===== Thâm niên (Đ.114) =====

    [Theory]
    [InlineData(2026, 0)]   // vừa vào làm đầu năm nay
    [InlineData(2021, 1)]   // đủ 5 năm
    [InlineData(2016, 2)]   // đủ 10 năm
    [InlineData(2015, 2)]   // 11 năm — vẫn 2 (chưa đủ 15)
    [InlineData(2011, 3)]   // đủ 15 năm
    public void SeniorityBonus_Adds_One_Day_Every_Five_Years(int joinYear, int expectedBonus)
    {
        AnnualLeaveEntitlement.SeniorityBonusDays(new DateTime(joinYear, 1, 1), 2026)
            .Should().Be(expectedBonus);
    }

    [Fact]
    public void SeniorityBonus_Is_Zero_When_Contract_Starts_After_The_Target_Year()
    {
        AnnualLeaveEntitlement.SeniorityBonusDays(new DateTime(2027, 5, 1), 2026).Should().Be(0);
    }

    // ===== Số tháng làm việc =====

    [Fact]
    public void Months_Worked_Is_Twelve_For_Open_Ended_Contract_Signed_Before_The_Year()
    {
        AnnualLeaveEntitlement.MonthsWorkedInYear(new DateTime(2020, 3, 15), null, 2026).Should().Be(12);
    }

    [Theory]
    [InlineData(1, 12)]     // vào 01/01 ⇒ cả năm
    [InlineData(6, 7)]      // vào 01/06 ⇒ T6…T12
    [InlineData(12, 1)]     // vào 01/12 ⇒ đúng 1 tháng
    public void Months_Worked_Counts_From_The_Joining_Month(int joinMonth, int expectedMonths)
    {
        AnnualLeaveEntitlement.MonthsWorkedInYear(new DateTime(2026, joinMonth, 1), null, 2026)
            .Should().Be(expectedMonths);
    }

    /// <summary>
    /// Đ.66 k.2 NĐ 145/2020 — tháng làm chưa trọn tính là 01 tháng nếu phủ từ 50% số ngày trở lên.
    /// Tháng 6 có 30 ngày: vào 16/06 phủ 15 ngày (đúng 50%) ⇒ tính; vào 17/06 phủ 14 ngày ⇒ không.
    /// </summary>
    [Theory]
    [InlineData(16, 7)]
    [InlineData(17, 6)]
    public void Partial_Month_Counts_Only_From_Fifty_Percent_Coverage(int joinDay, int expectedMonths)
    {
        AnnualLeaveEntitlement.MonthsWorkedInYear(new DateTime(2026, 6, joinDay), null, 2026)
            .Should().Be(expectedMonths);
    }

    [Fact]
    public void Months_Worked_Stops_At_The_Contract_End()
    {
        // HĐ hết hạn 30/06/2026 ⇒ chỉ T1…T6.
        AnnualLeaveEntitlement.MonthsWorkedInYear(
            new DateTime(2024, 1, 1), new DateTime(2026, 6, 30), 2026).Should().Be(6);
    }

    [Fact]
    public void Months_Worked_Is_Zero_When_The_Window_Misses_The_Year()
    {
        AnnualLeaveEntitlement.MonthsWorkedInYear(
            new DateTime(2020, 1, 1), new DateTime(2025, 12, 31), 2026).Should().Be(0);
        AnnualLeaveEntitlement.MonthsWorkedInYear(new DateTime(2027, 1, 1), null, 2026).Should().Be(0);
    }

    // ===== Chia quota =====

    [Fact]
    public void Full_Year_Keeps_The_Whole_Quota_Without_Rounding()
    {
        AnnualLeaveEntitlement.Prorate(12m, 2m, 12).Should().Be(14m);
    }

    [Theory]
    [InlineData(0, 12, 0, 0)]      // chưa làm tháng nào
    [InlineData(7, 12, 0, 7)]      // 12/12 × 7 = 7
    [InlineData(7, 12, 2, 8)]      // 14/12 × 7 = 8,17 ⇒ 8
    [InlineData(6, 12, 2, 7)]      // 14/12 × 6 = 7,0
    [InlineData(1, 12, 0, 1)]      // 12/12 × 1 = 1
    [InlineData(5, 12, 1, 5)]      // 13/12 × 5 = 5,42 ⇒ 5
    [InlineData(11, 12, 1, 12)]    // 13/12 × 11 = 11,92 ⇒ 12
    public void Prorate_Rounds_Half_Up_To_Whole_Days(
        int monthsWorked, int baseDays, int bonusDays, int expected)
    {
        AnnualLeaveEntitlement.Prorate(baseDays, bonusDays, monthsWorked).Should().Be(expected);
    }

    // ===== Gộp cả 3 bước =====

    /// <summary>LBAL-JOB02 — thâm niên 11 năm, làm cả năm ⇒ 12 + 2 = 14 ngày.</summary>
    [Fact]
    public void ForAnnual_Veteran_Working_Full_Year_Gets_Base_Plus_Seniority()
    {
        var grant = AnnualLeaveEntitlement.ForAnnual(new DateTime(2015, 1, 1), null, 2026);

        grant.MonthsWorked.Should().Be(12);
        grant.BaseDays.Should().Be(12m);
        grant.SeniorityBonusDays.Should().Be(2m);
        grant.TotalDays.Should().Be(14m);
    }

    /// <summary>LBAL-MID01 — vào làm tháng 12 ⇒ 12/12 × 1 = 1 ngày, KHÔNG cấp đủ 12.</summary>
    [Fact]
    public void ForAnnual_December_Hire_Gets_One_Day()
    {
        var grant = AnnualLeaveEntitlement.ForAnnual(new DateTime(2026, 12, 1), null, 2026);

        grant.MonthsWorked.Should().Be(1);
        grant.TotalDays.Should().Be(1m);
    }

    /// <summary>Phần tách Base/Bonus phải luôn cộng lại đúng TotalDays để bảng không lệch.</summary>
    [Theory]
    [InlineData(2015, 6)]
    [InlineData(2011, 3)]
    [InlineData(2026, 1)]
    public void ForAnnual_Split_Always_Sums_To_Total(int joinYear, int joinMonth)
    {
        var grant = AnnualLeaveEntitlement.ForAnnual(new DateTime(joinYear, joinMonth, 1), null, 2026);

        (grant.BaseDays + grant.SeniorityBonusDays).Should().Be(grant.TotalDays);
        grant.SeniorityBonusDays.Should().BeGreaterThanOrEqualTo(0m);
    }
}
