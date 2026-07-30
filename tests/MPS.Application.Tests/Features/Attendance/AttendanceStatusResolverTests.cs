using FluentAssertions;
using MPS.Application.Common.Services;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using AttendanceSettingEntity = MPS.Domain.Entities.Attendance.AttendanceSetting;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// Unit tests cho <see cref="AttendanceStatusResolver"/> — cover matrix của giờ chấm + shift + setting.
/// </summary>
public sealed class AttendanceStatusResolverTests
{
    private static readonly DateOnly WorkDate = new(2026, 7, 21);

    private static ShiftTemplate DayShift() => new()
    {
        Id = 1,
        Code = "DAY",
        Name = "Day Shift",
        StartTime = new TimeOnly(8, 0),
        EndTime = new TimeOnly(17, 0),
        BreakStartTime = new TimeOnly(12, 0),
        BreakEndTime = new TimeOnly(13, 0),
        StandardHours = 8m,
        StandardDays = 1m,
        AppliedAttendanceRuleId = 1,
        IsActive = true,
    };

    private static ShiftTemplate NightShift() => new()
    {
        Id = 2,
        Code = "NIGHT",
        Name = "Night Shift (cross-midnight)",
        StartTime = new TimeOnly(22, 0),
        EndTime = new TimeOnly(6, 0),
        StandardHours = 8m,
        StandardDays = 1m,
        AppliedAttendanceRuleId = 1,
        IsActive = true,
    };

    private static AttendanceSettingEntity SettingWithGrace(
        int lateGrace = 5,
        int earlyLeaveGrace = 5,
        int halfDayThreshold = 240) => new()
    {
        LateGraceMinutes = lateGrace,
        EarlyLeaveGraceMinutes = earlyLeaveGrace,
        HalfDayThresholdMinutes = halfDayThreshold,
    };

    private static AttendanceStatusResolver NewResolver() => new();

    [Fact]
    public void Resolve_Returns_Absent_When_CheckIn_Is_Null()
    {
        var shift = DayShift();
        var setting = SettingWithGrace();

        var result = NewResolver().Resolve(
            checkInAt: null,
            checkOutAt: null,
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Absent);
    }

    [Fact]
    public void Resolve_Returns_Present_When_OnTime_CheckIn_And_CheckOut()
    {
        var shift = DayShift();
        var setting = SettingWithGrace();

        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void Resolve_Returns_Present_When_Late_Within_Grace()
    {
        var shift = DayShift();
        var setting = SettingWithGrace(lateGrace: 5);

        // Check-in muộn 3 phút — grace = 5 → vẫn Present.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(3),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void Resolve_Returns_Late_When_Late_Exceeds_Grace()
    {
        var shift = DayShift();
        var setting = SettingWithGrace(lateGrace: 5, halfDayThreshold: 240);

        // Check-in muộn 10 phút — grace = 5 → Late.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(10),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Late);
    }

    [Fact]
    public void Resolve_Returns_HalfDay_When_Late_Exceeds_HalfDayThreshold()
    {
        var shift = DayShift();
        var setting = SettingWithGrace(halfDayThreshold: 240);

        // Check-in muộn 250 phút — halfDayT = 240 → HalfDay.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(250),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.HalfDay);
    }

    [Fact]
    public void Resolve_Returns_EarlyLeave_When_Early_Exceeds_Grace()
    {
        var shift = DayShift();
        var setting = SettingWithGrace(earlyLeaveGrace: 5);

        // Check-out sớm 10 phút — grace = 5 → EarlyLeave.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime).AddMinutes(-10),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.EarlyLeave);
    }

    [Fact]
    public void Resolve_Returns_HalfDay_When_Both_Late_And_Early_Exceed_Grace()
    {
        var shift = DayShift();
        var setting = SettingWithGrace(lateGrace: 5, earlyLeaveGrace: 5, halfDayThreshold: 240);

        // Muộn 10 + sớm 10 (đều vượt grace nhưng chưa đến halfDayThreshold) → HalfDay.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(10),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime).AddMinutes(-10),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.HalfDay);
    }

    [Fact]
    public void Resolve_Returns_HalfDay_When_EarlyLeave_Exceeds_HalfDayThreshold()
    {
        var shift = DayShift();
        var setting = SettingWithGrace(halfDayThreshold: 240);

        // Về sớm 250 phút — vượt halfDayT.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime).AddMinutes(-250),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.HalfDay);
    }

    [Fact]
    public void Resolve_Returns_Present_When_CheckOut_Null_And_CheckIn_OnTime()
    {
        var shift = DayShift();
        var setting = SettingWithGrace();

        // Chưa check-out — earlyMin = 0, và check-in đúng giờ → Present.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime),
            checkOutAt: null,
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void Resolve_Uses_Default_Grace_When_Setting_Fields_Are_Null()
    {
        var shift = DayShift();
        var setting = new AttendanceSettingEntity(); // all grace fields null → dùng default 5/5/240

        // Muộn 10 (> default 5) → Late.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(10),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Late);
    }

    [Fact]
    public void Resolve_Uses_Default_Grace_When_Setting_Is_Null()
    {
        var shift = DayShift();

        // Setting = null hoàn toàn → dùng default 5/5/240.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(300),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: null);

        // 300 > default halfDayThreshold 240 → HalfDay.
        result.Should().Be(AttendanceStatus.HalfDay);
    }

    [Fact]
    public void Resolve_Handles_CrossMidnight_Shift_Correctly()
    {
        var shift = NightShift(); // 22:00 → 06:00 hôm sau
        var setting = SettingWithGrace(earlyLeaveGrace: 5);

        // Check-in đúng giờ 22:00, check-out sớm 10 phút (5:50 sáng hôm sau).
        var plannedEnd = WorkDate.AddDays(1).ToDateTime(shift.EndTime);
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime),
            checkOutAt: plannedEnd.AddMinutes(-10),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        // earlyMin = 10 phút > grace 5 → EarlyLeave (không âm).
        result.Should().Be(AttendanceStatus.EarlyLeave);
    }

    [Fact]
    public void Resolve_Handles_CrossMidnight_Shift_OnTime_As_Present()
    {
        var shift = NightShift();
        var setting = SettingWithGrace();

        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime),
            checkOutAt: WorkDate.AddDays(1).ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void Resolve_Boundary_Late_Exactly_Grace_Is_Present()
    {
        var shift = DayShift();
        var setting = SettingWithGrace(lateGrace: 5);

        // Muộn đúng 5 phút — grace = 5, điều kiện là "> grace" → vẫn Present.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(5),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void Resolve_Boundary_Late_Exactly_HalfDayThreshold_Is_HalfDay()
    {
        var shift = DayShift();
        var setting = SettingWithGrace(halfDayThreshold: 240);

        // Muộn đúng 240 phút — halfDayT = 240, điều kiện là ">= halfDayT" → HalfDay.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(240),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.HalfDay);
    }

    [Fact]
    public void Resolve_Ignores_Early_CheckIn_Before_Shift_Start()
    {
        var shift = DayShift();
        var setting = SettingWithGrace();

        // Check-in sớm 30 phút — lateMin = 0 (max(0, negative) = 0) → Present.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime).AddMinutes(-30),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void Resolve_Ignores_Late_CheckOut_After_Shift_End()
    {
        var shift = DayShift();
        var setting = SettingWithGrace();

        // Check-out muộn 30 phút sau ca — earlyMin = 0 → Present.
        var result = NewResolver().Resolve(
            checkInAt: WorkDate.ToDateTime(shift.StartTime),
            checkOutAt: WorkDate.ToDateTime(shift.EndTime).AddMinutes(30),
            shift: shift,
            workDate: WorkDate,
            setting: setting);

        result.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void Defaults_Match_Plan_Values()
    {
        AttendanceGraceDefaults.LateGraceMinutes.Should().Be(5);
        AttendanceGraceDefaults.EarlyLeaveGraceMinutes.Should().Be(5);
        AttendanceGraceDefaults.HalfDayThresholdMinutes.Should().Be(240);
    }
}
