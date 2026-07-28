using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Entities.Attendance;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Services;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Tests cho <see cref="EmployeeDayOffChecker"/> — quyết định kiosk có cho nhân viên
/// vào trang chấm công hay không. Điểm mấu chốt: ngày lễ / cuối tuần MÀ có phân ca thì
/// vẫn phải đi làm, còn đơn nghỉ phép đã duyệt thì chặn bất kể có ca hay không.
/// </summary>
public sealed class EmployeeDayOffCheckerTests
{
    private const int EmployeeId = 42;
    private const int OtherEmployeeId = 99;

    /// <summary>Thứ Tư 15/07/2026 — ngày thường.</summary>
    private static readonly DateOnly Weekday = new(2026, 7, 15);

    /// <summary>Thứ Bảy 18/07/2026.</summary>
    private static readonly DateOnly Saturday = new(2026, 7, 18);

    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"day-off-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static void AddLeave(
        AppDbContext db,
        DateOnly start,
        DateOnly end,
        AttendanceEnums.LeaveRequestStatus status,
        bool isPaid,
        string leaveTypeName,
        int employeeId = EmployeeId)
    {
        var leaveTypeId = db.LeaveTypes.Count() + 1;
        db.LeaveTypes.Add(new LeaveType
        {
            Id = leaveTypeId,
            Code = $"TYPE_{leaveTypeId}",
            Name = leaveTypeName,
            IsPaid = isPaid,
        });
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeInformationId = employeeId,
            LeaveTypeId = leaveTypeId,
            StartDate = start,
            EndDate = end,
            IsPaid = isPaid,
            Status = status,
        });
    }

    private static void AddHoliday(AppDbContext db, DateOnly start, DateOnly end, string name, bool isActive = true) =>
        db.PublicHolidays.Add(new PublicHoliday
        {
            Name = name,
            HolidayDate = start,
            HolidayEndDate = end,
            IsActive = isActive,
        });

    private static void AddShift(AppDbContext db, DateOnly workDate, int employeeId = EmployeeId) =>
        db.EmployeeShiftAssignments.Add(new EmployeeShiftAssignment
        {
            EmployeeInformationId = employeeId,
            ShiftTemplateId = 1,
            WorkDate = workDate,
        });

    private static async Task<DayOffStatusDto> CheckAsync(AppDbContext db, DateOnly date)
    {
        await db.SaveChangesAsync();
        return await new EmployeeDayOffChecker(db).CheckAsync(EmployeeId, date);
    }

    [Fact]
    public async Task CheckAsync_ReturnsLeave_WhenApprovedPaidLeaveCoversDate()
    {
        using var db = NewContext();
        AddLeave(db, Weekday, Weekday, AttendanceEnums.LeaveRequestStatus.Approved, isPaid: true, "Nghỉ phép năm");

        var result = await CheckAsync(db, Weekday);

        result.IsDayOff.Should().BeTrue();
        result.Reason.Should().Be(DayOffReasons.Leave);
        result.DetailName.Should().Be("Nghỉ phép năm");
        result.Message.Should().Contain("Nghỉ phép năm");
    }

    [Fact]
    public async Task CheckAsync_ReturnsLeave_WhenApprovedUnpaidLeaveCoversDate()
    {
        using var db = NewContext();
        AddLeave(db, Weekday, Weekday, AttendanceEnums.LeaveRequestStatus.Approved, isPaid: false, "Nghỉ không lương");

        var result = await CheckAsync(db, Weekday);

        result.IsDayOff.Should().BeTrue();
        result.Reason.Should().Be(DayOffReasons.Leave);
    }

    [Fact]
    public async Task CheckAsync_BlocksLeave_EvenWhenEmployeeStillHasShiftAssignment()
    {
        using var db = NewContext();
        AddLeave(db, Weekday, Weekday, AttendanceEnums.LeaveRequestStatus.Approved, isPaid: true, "Nghỉ ốm");
        AddShift(db, Weekday);

        var result = await CheckAsync(db, Weekday);

        result.Reason.Should().Be(DayOffReasons.Leave);
    }

    [Theory]
    [InlineData(AttendanceEnums.LeaveRequestStatus.Pending)]
    [InlineData(AttendanceEnums.LeaveRequestStatus.Rejected)]
    [InlineData(AttendanceEnums.LeaveRequestStatus.Cancelled)]
    public async Task CheckAsync_IgnoresLeave_WhenNotApproved(AttendanceEnums.LeaveRequestStatus status)
    {
        using var db = NewContext();
        AddLeave(db, Weekday, Weekday, status, isPaid: true, "Nghỉ phép năm");

        var result = await CheckAsync(db, Weekday);

        result.IsDayOff.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_IgnoresLeave_OfAnotherEmployee()
    {
        using var db = NewContext();
        AddLeave(db, Weekday, Weekday, AttendanceEnums.LeaveRequestStatus.Approved, isPaid: true,
            "Nghỉ phép năm", employeeId: OtherEmployeeId);

        var result = await CheckAsync(db, Weekday);

        result.IsDayOff.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ReturnsPublicHoliday_WhenHolidayAndNoShiftAssigned()
    {
        using var db = NewContext();
        AddHoliday(db, Weekday, Weekday, "Quốc khánh");

        var result = await CheckAsync(db, Weekday);

        result.IsDayOff.Should().BeTrue();
        result.Reason.Should().Be(DayOffReasons.PublicHoliday);
        result.DetailName.Should().Be("Quốc khánh");
    }

    [Fact]
    public async Task CheckAsync_ReturnsNotDayOff_WhenHolidayButShiftAssigned()
    {
        using var db = NewContext();
        AddHoliday(db, Weekday, Weekday, "Quốc khánh");
        AddShift(db, Weekday);

        var result = await CheckAsync(db, Weekday);

        result.IsDayOff.Should().BeFalse();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_ReturnsPublicHoliday_WhenDateInsideMultiDayHolidayRange()
    {
        using var db = NewContext();
        AddHoliday(db, Weekday.AddDays(-2), Weekday.AddDays(2), "Tết Dương lịch");

        var result = await CheckAsync(db, Weekday);

        result.Reason.Should().Be(DayOffReasons.PublicHoliday);
    }

    [Fact]
    public async Task CheckAsync_IgnoresHoliday_WhenInactive()
    {
        using var db = NewContext();
        AddHoliday(db, Weekday, Weekday, "Ngày lễ đã huỷ", isActive: false);

        var result = await CheckAsync(db, Weekday);

        result.IsDayOff.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ReturnsWeeklyRest_WhenWeekendAndNoShiftAssigned()
    {
        using var db = NewContext();

        var result = await CheckAsync(db, Saturday);

        result.IsDayOff.Should().BeTrue();
        result.Reason.Should().Be(DayOffReasons.WeeklyRest);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNotDayOff_WhenWeekendButShiftAssigned()
    {
        using var db = NewContext();
        AddShift(db, Saturday);

        var result = await CheckAsync(db, Saturday);

        result.IsDayOff.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ReturnsNotDayOff_OnPlainWeekday()
    {
        using var db = NewContext();

        var result = await CheckAsync(db, Weekday);

        result.IsDayOff.Should().BeFalse();
        result.Reason.Should().BeNull();
        result.Message.Should().BeNull();
        result.Date.Should().Be(Weekday);
    }

    [Fact]
    public async Task CheckAsync_IgnoresShift_OfAnotherEmployeeOnHoliday()
    {
        using var db = NewContext();
        AddHoliday(db, Weekday, Weekday, "Giỗ Tổ");
        AddShift(db, Weekday, employeeId: OtherEmployeeId);

        var result = await CheckAsync(db, Weekday);

        result.Reason.Should().Be(DayOffReasons.PublicHoliday);
    }
}
