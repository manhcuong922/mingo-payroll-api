using FluentAssertions;
using MPS.Application.Features.Contract.Common;

namespace MPS.Application.Tests.Features.ContractAppendix.Common;

/// <summary>
/// Unit test cho WorkingDayCalculator — phục vụ rule Điều 33 (báo trước ≥ 3 ngày làm việc).
/// Mỗi case dùng ngày tham chiếu cố định để tránh phụ thuộc lịch hệ thống.
/// </summary>
public sealed class WorkingDayCalculatorTests
{
    /// <summary>
    /// T2 đến T6 cùng tuần = đếm T2, T3, T4, T5 (4 ngày làm việc, end exclusive).
    /// (T6 không tính vì hàm dùng nửa-mở [from, to).)
    /// </summary>
    [Fact]
    public void Count_From_Monday_To_Friday_Same_Week_Returns_4_Working_Days()
    {
        // 2026-06-08 là T2, 2026-06-12 là T6.
        var from = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc);

        WorkingDayCalculator.CountWorkingDaysBetween(from, to).Should().Be(4);
    }

    /// <summary>T7 + CN không tính là ngày làm việc.</summary>
    [Fact]
    public void Count_Skips_Saturday_And_Sunday()
    {
        // 2026-06-13 là T7, 2026-06-15 là T2 → khoảng [T7..T2) gồm T7+CN → 0 working days.
        var from = new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        WorkingDayCalculator.CountWorkingDaysBetween(from, to).Should().Be(0);
    }

    /// <summary>from == to → 0; from > to → 0.</summary>
    [Fact]
    public void Count_Returns_Zero_When_Range_Empty_Or_Reversed()
    {
        var d = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        WorkingDayCalculator.CountWorkingDaysBetween(d, d).Should().Be(0);
        WorkingDayCalculator.CountWorkingDaysBetween(d, d.AddDays(-3)).Should().Be(0);
    }

    /// <summary>
    /// Rule Điều 33 — báo từ T2 → có hiệu lực T5 cùng tuần là OK (gap = T2,T3,T4 = 3 working days).
    /// </summary>
    [Fact]
    public void HasMinimumWorkingDayGap_Returns_True_When_Notice_Monday_Effective_Thursday()
    {
        var notice = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc); // Monday
        var effective = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc); // Thursday

        WorkingDayCalculator.HasMinimumWorkingDayGap(notice, effective, 3).Should().BeTrue();
    }

    /// <summary>
    /// Rule Điều 33 fail: báo T5, có hiệu lực T2 tuần sau → khoảng có T5+T6 = 2 working days, T7/CN không tính.
    /// </summary>
    [Fact]
    public void HasMinimumWorkingDayGap_Returns_False_When_Only_2_Working_Days_Between()
    {
        var notice = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc); // Thursday
        var effective = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc); // Monday next week

        WorkingDayCalculator.HasMinimumWorkingDayGap(notice, effective, 3).Should().BeFalse();
    }

    /// <summary>
    /// Báo T6, có hiệu lực T4 tuần sau (T6 → T2,T3 → 2 working days < 3) → fail. Verify cuối tuần không
    /// được tính nhầm.
    /// </summary>
    [Fact]
    public void HasMinimumWorkingDayGap_Friday_Notice_Tuesday_Next_Week_Is_Not_Enough()
    {
        var notice = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc); // Friday
        var effective = new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc); // Tuesday next week

        WorkingDayCalculator.HasMinimumWorkingDayGap(notice, effective, 3).Should().BeFalse();
    }
}
