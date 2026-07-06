using FluentAssertions;
using MPS.Application.Common.Interfaces;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Ma trận test cho <see cref="WorkHourSplitter"/> — phủ ca thường, ca đêm,
/// cross-midnight, break trùng / không trùng night window, edge cases.
///
/// Night window mặc định: 22h–06h (Điều 106 BLLĐ).
/// </summary>
public sealed class WorkHourSplitterTests
{
    private static readonly TimeOnly NightStart = new(22, 0);
    private static readonly TimeOnly NightEnd = new(6, 0);

    private static WorkHourSplitInput Build(
        string checkIn, string checkOut,
        string? breakStart = null, string? breakEnd = null)
        => new(
            TimeOnly.Parse(checkIn),
            TimeOnly.Parse(checkOut),
            breakStart is null ? null : TimeOnly.Parse(breakStart),
            breakEnd is null ? null : TimeOnly.Parse(breakEnd),
            NightStart,
            NightEnd);

    private readonly WorkHourSplitter _splitter = new();

    // ── Ca thường, không cross midnight ─────────────────────────────────────
    [Fact]
    public void Office_shift_08_to_17_with_break_12_13_is_all_day()
    {
        var r = _splitter.Split(Build("08:00", "17:00", "12:00", "13:00"));
        r.DayHours.Should().Be(8.00m);
        r.NightHours.Should().Be(0m);
        r.Total.Should().Be(8.00m);
    }

    [Fact]
    public void Shift_no_break_08_to_17_returns_9_day_hours()
    {
        var r = _splitter.Split(Build("08:00", "17:00"));
        r.DayHours.Should().Be(9.00m);
        r.NightHours.Should().Be(0m);
    }

    // ── Ca đêm, cross midnight ─────────────────────────────────────────────
    [Fact]
    public void Night_shift_22_to_06_is_all_night()
    {
        var r = _splitter.Split(Build("22:00", "06:00"));
        r.DayHours.Should().Be(0m);
        r.NightHours.Should().Be(8.00m);
    }

    [Fact]
    public void Mixed_shift_18_to_02_splits_4_day_4_night()
    {
        var r = _splitter.Split(Build("18:00", "02:00"));
        r.DayHours.Should().Be(4.00m);
        r.NightHours.Should().Be(4.00m);
    }

    [Fact]
    public void Early_morning_shift_04_to_12_splits_2_night_6_day()
    {
        var r = _splitter.Split(Build("04:00", "12:00"));
        r.NightHours.Should().Be(2.00m);
        r.DayHours.Should().Be(6.00m);
    }

    // ── Break trong night window ───────────────────────────────────────────
    [Fact]
    public void Cross_midnight_shift_with_break_in_night_window_subtracts_night_only()
    {
        // Ca 18h-02h, break 23h-00h (1h) → Day=4, Night=3
        var r = _splitter.Split(Build("18:00", "02:00", "23:00", "00:00"));
        r.DayHours.Should().Be(4.00m);
        r.NightHours.Should().Be(3.00m);
    }

    [Fact]
    public void Break_in_day_portion_does_not_reduce_night()
    {
        // Ca 18h-02h, break 19h-20h (1h) → Day=3, Night=4
        var r = _splitter.Split(Build("18:00", "02:00", "19:00", "20:00"));
        r.DayHours.Should().Be(3.00m);
        r.NightHours.Should().Be(4.00m);
    }

    // ── Edge cases ─────────────────────────────────────────────────────────
    [Fact]
    public void Shift_ending_exactly_at_night_start_has_zero_night()
    {
        var r = _splitter.Split(Build("21:00", "22:00"));
        r.DayHours.Should().Be(1.00m);
        r.NightHours.Should().Be(0m);
    }

    [Fact]
    public void Shift_starting_exactly_at_night_end_has_zero_night()
    {
        var r = _splitter.Split(Build("06:00", "14:00"));
        r.DayHours.Should().Be(8.00m);
        r.NightHours.Should().Be(0m);
    }

    [Fact]
    public void Shift_entirely_in_night_window_no_break()
    {
        // Ca 23h-04h next day = 5h đêm hết
        var r = _splitter.Split(Build("23:00", "04:00"));
        r.NightHours.Should().Be(5.00m);
        r.DayHours.Should().Be(0m);
    }

    [Fact]
    public void Half_hour_increments_compute_correctly()
    {
        // Ca 21:30-23:30 = 30 min ngày (21:30-22:00) + 90 min đêm (22:00-23:30) = 0.5 + 1.5
        var r = _splitter.Split(Build("21:30", "23:30"));
        r.DayHours.Should().Be(0.50m);
        r.NightHours.Should().Be(1.50m);
    }

    [Fact]
    public void Full_24h_shift_yields_16_day_8_night()
    {
        // Ca 06h-06h (24h) — toàn bộ 1 ngày.
        // Day: 06h-22h = 16h. Night: 22h-06h = 8h.
        var r = _splitter.Split(Build("06:00", "06:00"));
        r.DayHours.Should().Be(16.00m);
        r.NightHours.Should().Be(8.00m);
        r.Total.Should().Be(24.00m);
    }

    [Fact]
    public void Break_outside_shift_range_is_ignored()
    {
        // Ca 08h-17h, break "03:00-04:00" rõ ràng ngoài range → bỏ qua
        var r = _splitter.Split(Build("08:00", "17:00", "03:00", "04:00"));
        r.DayHours.Should().Be(9.00m);
        r.NightHours.Should().Be(0m);
    }
}
