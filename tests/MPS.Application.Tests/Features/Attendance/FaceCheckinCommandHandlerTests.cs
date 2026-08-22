using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Services;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.FaceRegistration.Commands;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Application.Settings;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using Xunit;

namespace MPS.Application.Tests.Features.Attendance;

public sealed class FaceCheckinCommandHandlerTests
{
    private const int EmployeeInformationId = 7;
    private static readonly DateOnly Today = new(2026, 8, 3); // Monday
    private static readonly TimeZoneInfo VnZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private readonly Mock<IAttendanceRecordRepository> _attendanceRepository = new();
    private readonly Mock<IEmployeeShiftAssignmentRepository> _shiftAssignmentRepository = new();
    private readonly Mock<IEmployeeInformationRepository> _employeeRepository = new();
    private readonly Mock<IAttendanceSettingRepository> _settingRepository = new();
    private readonly Mock<IAttendanceStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IWorkHourSplitter> _splitter = new();
    private readonly Mock<IComplianceLookupService> _complianceLookup = new();
    private readonly Mock<IEmployeeDayOffChecker> _dayOffChecker = new();
    private readonly Mock<IOvertimeRequestRepository> _overtimeRepository = new();
    private readonly Mock<ILateEarlyRequestRepository> _lateEarlyRepository = CreateEmptyLateEarlyRepo();

    private static Mock<ILateEarlyRequestRepository> CreateEmptyLateEarlyRepo()
    {
        var mock = new Mock<ILateEarlyRequestRepository>();
        mock.Setup(r => r.GetApprovedByEmployeeAndDateAsync(
                It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LateEarlyRequest>());
        return mock;
    }
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<ILogger<FaceCheckinCommandHandler>> _logger = new();

    private AttendanceCheckinSettings _checkinSettings = new();

    private FaceCheckinCommandHandler CreateSut() => new(
        _attendanceRepository.Object,
        _shiftAssignmentRepository.Object,
        _employeeRepository.Object,
        _settingRepository.Object,
        _statusResolver.Object,
        _currentUser.Object,
        _splitter.Object,
        _complianceLookup.Object,
        _dayOffChecker.Object,
        _overtimeRepository.Object,
        new LateEarlyExcuseResolver(_lateEarlyRepository.Object),
        _clock.Object,
        _logger.Object,
        Microsoft.Extensions.Options.Options.Create(_checkinSettings));

    private static ShiftTemplate DayShift() => new()
    {
        Id = 1,
        Code = "DAY",
        Name = "Ca ngày",
        StartTime = new TimeOnly(8, 0),
        EndTime = new TimeOnly(17, 0),
    };

    private static EmployeeShiftAssignment AssignmentFor(ShiftTemplate shift, DateOnly workDate) => new()
    {
        Id = 1,
        EmployeeInformationId = EmployeeInformationId,
        ShiftTemplateId = shift.Id,
        ShiftTemplate = shift,
        WorkDate = workDate,
    };

    /// <summary>UTC instant tương ứng với 1 giờ VN cụ thể trong ngày <see cref="Today"/>.</summary>
    private static DateTime UtcAt(int hour, int minute = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(Today.Year, Today.Month, Today.Day, hour, minute, 0, DateTimeKind.Unspecified),
            VnZone);

    private void ArrangeCommon(bool hasActiveUser = true, bool isDayOff = false)
    {
        _employeeRepository.Setup(r => r.HasActiveUserAsync(EmployeeInformationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasActiveUser);
        _dayOffChecker.Setup(c => c.CheckAsync(EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DayOffStatusDto(
                Today, isDayOff, isDayOff ? DayOffReasons.Leave : null, isDayOff ? "Nghỉ phép đã duyệt." : null, null));
        _settingRepository.Setup(r => r.GetOrCreateDefaultAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceSetting());
        _statusResolver.Setup(r => r.Resolve(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<ShiftTemplate>(), It.IsAny<DateOnly>(), It.IsAny<AttendanceSetting?>()))
            .Returns(AttendanceStatus.Present);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
        // Mặc định không có OT nào — test riêng cho luồng OT sẽ override lại.
        _overtimeRepository.Setup(r => r.GetActiveOnDateForEmployeeAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private static FaceCheckinCommand Command(DateTime checkinTimeUtc) =>
        new(EmployeeInformationId.ToString(), checkinTimeUtc, 0.95f, 1, "kiosk");

    [Fact]
    public async Task Handle_Fails_WhenEmployeeIdNotParsable()
    {
        var result = await CreateSut().Handle(
            new FaceCheckinCommand("not-an-int", DateTime.UtcNow, 0.9f, 1, "kiosk"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.CheckinType.Should().Be(FaceCheckinType.Failed);
    }

    [Fact]
    public async Task Handle_Fails_WhenEmployeeHasNoActiveUser()
    {
        ArrangeCommon(hasActiveUser: false);

        var result = await CreateSut().Handle(Command(UtcAt(8)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("active");
    }

    [Fact]
    public async Task Handle_Fails_WhenNoShiftAssignedAndNotDayOff()
    {
        ArrangeCommon();
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateSut().Handle(Command(UtcAt(8)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("chưa được phân ca");
    }

    [Fact]
    public async Task Handle_ReportsDayOffReason_WhenNoShiftAndOnApprovedLeave()
    {
        ArrangeCommon(isDayOff: true);
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateSut().Handle(Command(UtcAt(8)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Nghỉ phép đã duyệt.");
    }

    [Fact]
    public async Task Handle_Blocks_WhenOpeningNewShiftOnApprovedLeaveDay()
    {
        // Case 1 gate: có ca nhưng đang nghỉ phép đã duyệt → không cho mở ca mới.
        ArrangeCommon(isDayOff: true);
        var shift = DayShift();
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);

        var result = await CreateSut().Handle(Command(UtcAt(8)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Nghỉ phép đã duyệt.");
        _attendanceRepository.Verify(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Fails_WhenScanningBeforeShiftOpens()
    {
        ArrangeCommon();
        var shift = DayShift(); // opens 07:00 (60' early), scan at 05:00 is too early
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);

        var result = await CreateSut().Handle(Command(UtcAt(5)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Chưa đến giờ chấm công");
    }

    [Fact]
    public async Task Handle_Fails_WhenScanningAfterShiftClosed()
    {
        ArrangeCommon();
        var shift = DayShift(); // closes 17:00 + 360' = 23:00
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);

        var result = await CreateSut().Handle(Command(UtcAt(23, 30)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Đã quá giờ chấm công");
    }

    [Fact]
    public async Task Handle_CreatesCheckin_WhenNoExistingRecordAndWithinWindow()
    {
        ArrangeCommon();
        var shift = DayShift();
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        _attendanceRepository.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(123);

        var result = await CreateSut().Handle(Command(UtcAt(8)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.Checkin);
        result.AttendanceLogId.Should().Be("123");
        _attendanceRepository.Verify(r => r.AddAsync(
            It.Is<AttendanceRecord>(a => a.CheckInAt != null && a.CheckOutAt == null && a.Status == AttendanceStatus.Present),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Hạn chót MỞ ca mới (LateCheckinMinutes), tách khỏi hạn đóng ca ──────────────

    [Fact]
    public async Task Handle_Fails_WhenOpeningNewShiftAfterCheckinDeadline()
    {
        // Ca 08:00–17:00, cap 240' ⇒ hạn mở ca 12:00. Quét lần đầu lúc 14:00 (vẫn trong cửa sổ
        // đóng ca tới 23:00) KHÔNG được mở ca mới: bản ghi đó sẽ ra HalfDay = 0.5 công hưởng lương
        // trong khi giờ công thực tế bằng 0 vì nằm trọn ngoài khung ca.
        ArrangeCommon();
        var shift = DayShift();
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);

        var result = await CreateSut().Handle(Command(UtcAt(14)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Đã quá hạn chấm công VÀO ca");
        result.Message.Should().Contain("12:00");
        _attendanceRepository.Verify(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CreatesCheckin_ExactlyAtCheckinDeadline()
    {
        // Biên: đúng 12:00 vẫn mở được ca. Cap 240' bằng đúng trần LateEarlyRequest.Minutes nên NV
        // có đơn xin đi trễ 4 tiếng ĐÃ DUYỆT không bị chặn nhầm.
        ArrangeCommon();
        var shift = DayShift();
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        _attendanceRepository.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(124);

        var result = await CreateSut().Handle(Command(UtcAt(12)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.Checkin);
    }

    [Fact]
    public async Task Handle_ClampsCheckinDeadlineToShiftEnd_ForShiftShorterThanCap()
    {
        // Ca 4 tiếng (08:00–12:00): StartAt + 240' = đúng giờ TAN ca, không được nới quá.
        ArrangeCommon();
        var shift = new ShiftTemplate
        {
            Id = 3,
            Code = "HALF",
            Name = "Ca nửa ngày",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
        };
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);

        // 12:30 vẫn trong cửa sổ ĐÓNG ca (12:00 + 360' = 18:00) nhưng đã quá giờ tan ca.
        var result = await CreateSut().Handle(Command(UtcAt(12, 30)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Đã quá hạn chấm công VÀO ca");
        _attendanceRepository.Verify(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_StillChecksOut_LongAfterShiftEnd_WhenRecordAlreadyOpen()
    {
        // Hạn mở ca KHÔNG được đụng tới nhánh chấm RA: OT trần 4h/ngày nên NV tan ca sau OT quét
        // muộn hơn giờ tan ca chính vài tiếng là bình thường (xem LateCheckoutMinutes = 360').
        ArrangeCommon();
        ArrangeOpenShift();
        ArrangeCheckoutDependencies();

        var result = await CreateSut().Handle(Command(UtcAt(20)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.Checkout);
    }

    [Fact]
    public async Task Handle_PicksTodayShift_WhenYesterdayNightShiftIsPastItsCheckinDeadline()
    {
        // Ca đêm hôm qua (22:00–06:00) còn nằm trong cửa sổ đóng ca tới 12:00 hôm nay, nhưng hạn mở
        // ca của nó đã hết từ 02:00. NV quét 08:30 để VÀO ca ngày hôm nay không được bị ca đêm quá
        // hạn kia nuốt mất lượt — đây là lý do "quá hạn" bị xếp cuối bảng thay vì loại thẳng.
        ArrangeCommon();
        var yesterday = Today.AddDays(-1);
        var nightShift = new ShiftTemplate
        {
            Id = 2,
            Code = "NIGHT",
            Name = "Ca đêm",
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0),
        };
        var dayShift = DayShift();
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(nightShift, yesterday)]);
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(dayShift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        _attendanceRepository.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(125);

        var result = await CreateSut().Handle(Command(UtcAt(8, 30)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.Checkin);
        result.ShiftName.Should().Be(dayShift.Name);
        _attendanceRepository.Verify(r => r.AddAsync(
            It.Is<AttendanceRecord>(a => a.ShiftTemplateId == dayShift.Id && a.WorkDate == Today),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsCooldown_WhenCheckingOutTooSoonAfterCheckin()
    {
        ArrangeCommon();
        var shift = DayShift();
        var checkInAt = TimeZoneInfo.ConvertTimeFromUtc(UtcAt(8), VnZone);
        var existing = new AttendanceRecord
        {
            Id = 55,
            EmployeeInformationId = EmployeeInformationId,
            WorkDate = Today,
            ShiftTemplateId = shift.Id,
            ShiftTemplate = shift,
            CheckInAt = checkInAt,
            CheckOutAt = null,
        };
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Quét lại chỉ 1 phút sau check-in — dưới ngưỡng cooldown 5 phút.
        var result = await CreateSut().Handle(Command(UtcAt(8, 1)), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.CheckinType.Should().Be(FaceCheckinType.Cooldown);
        result.CooldownSecondsRemaining.Should().NotBeNull();
        _attendanceRepository.Verify(r => r.UpdateAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ChecksOut_WhenCooldownElapsed()
    {
        ArrangeCommon();
        var shift = DayShift();
        var checkInAt = TimeZoneInfo.ConvertTimeFromUtc(UtcAt(8), VnZone);
        var existing = new AttendanceRecord
        {
            Id = 55,
            EmployeeInformationId = EmployeeInformationId,
            WorkDate = Today,
            ShiftTemplateId = shift.Id,
            ShiftTemplate = shift,
            CheckInAt = checkInAt,
            CheckOutAt = null,
        };
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _complianceLookup.Setup(c => c.GetLaborLawConstantsAtAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Application.Common.Models.Compliance.LaborLawConstantsSnapshot(
                VersionId: 1,
                ValidFrom: Today,
                ProbationSalaryMinPercent: 0m,
                OvertimeNightExtraPercent: 0m,
                NightWorkSurchargePercent: 0m,
                OvertimeNormalUpliftPercent: 0m,
                NightStartTime: new TimeOnly(22, 0),
                NightEndTime: new TimeOnly(6, 0),
                StandardWorkingHoursPerDay: 8m,
                MaxRegularHoursPerWeek: 48m,
                StandardWorkingDaysPerMonth: 26m,
                AnnualLeaveMinDays: 12,
                WorkRateNormalFloor: 1m,
                WorkRateWeeklyRestFloor: 1m,
                WorkRateHolidayFloor: 1m,
                OvertimeMaxHoursPerDay: 4m,
                OvertimeMaxHoursPerMonth: 40m,
                OvertimeMaxHoursPerYear: 200,
                LegalReference: "test"));
        _splitter.Setup(s => s.Split(It.IsAny<WorkHourDateTimeSplitInput>()))
            .Returns(new WorkHourBreakdown(8m, 0m));
        _attendanceRepository.Setup(r => r.UpdateAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Quét lại 17:00 (9 tiếng sau check-in) — đủ qua cooldown 5 phút.
        var result = await CreateSut().Handle(Command(UtcAt(17)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.Checkout);
        _attendanceRepository.Verify(r => r.UpdateAsync(
            It.Is<AttendanceRecord>(a => a.CheckOutAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyComplete_WhenShiftAlreadyHasCheckoutRecorded()
    {
        ArrangeCommon();
        var shift = DayShift();
        var checkInAt = TimeZoneInfo.ConvertTimeFromUtc(UtcAt(8), VnZone);
        var checkOutAt = TimeZoneInfo.ConvertTimeFromUtc(UtcAt(17), VnZone);
        var existing = new AttendanceRecord
        {
            Id = 55,
            EmployeeInformationId = EmployeeInformationId,
            WorkDate = Today,
            ShiftTemplateId = shift.Id,
            ShiftTemplate = shift,
            CheckInAt = checkInAt,
            CheckOutAt = checkOutAt,
        };
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateSut().Handle(Command(UtcAt(18)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.AlreadyComplete);
        _attendanceRepository.Verify(r => r.UpdateAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotCheckDayOff_OnCheckoutBranch()
    {
        // Ghi lại hành vi HIỆN TẠI (đã bàn với business): checkout KHÔNG tái kiểm tra day-off,
        // kể cả khi đơn nghỉ phép được duyệt hồi tố giữa ca — tránh kẹt record CheckOutAt=null
        // vĩnh viễn vì không có job tự đóng ca. Test này là tài liệu hoá quyết định, không phải
        // khẳng định đây là hành vi lý tưởng.
        ArrangeCommon(isDayOff: true);
        var shift = DayShift();
        var checkInAt = TimeZoneInfo.ConvertTimeFromUtc(UtcAt(8), VnZone);
        var existing = new AttendanceRecord
        {
            Id = 55,
            EmployeeInformationId = EmployeeInformationId,
            WorkDate = Today,
            ShiftTemplateId = shift.Id,
            ShiftTemplate = shift,
            CheckInAt = checkInAt,
            CheckOutAt = null,
        };
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _complianceLookup.Setup(c => c.GetLaborLawConstantsAtAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Application.Common.Models.Compliance.LaborLawConstantsSnapshot(
                VersionId: 1,
                ValidFrom: Today,
                ProbationSalaryMinPercent: 0m,
                OvertimeNightExtraPercent: 0m,
                NightWorkSurchargePercent: 0m,
                OvertimeNormalUpliftPercent: 0m,
                NightStartTime: new TimeOnly(22, 0),
                NightEndTime: new TimeOnly(6, 0),
                StandardWorkingHoursPerDay: 8m,
                MaxRegularHoursPerWeek: 48m,
                StandardWorkingDaysPerMonth: 26m,
                AnnualLeaveMinDays: 12,
                WorkRateNormalFloor: 1m,
                WorkRateWeeklyRestFloor: 1m,
                WorkRateHolidayFloor: 1m,
                OvertimeMaxHoursPerDay: 4m,
                OvertimeMaxHoursPerMonth: 40m,
                OvertimeMaxHoursPerYear: 200,
                LegalReference: "test"));
        _splitter.Setup(s => s.Split(It.IsAny<WorkHourDateTimeSplitInput>()))
            .Returns(new WorkHourBreakdown(8m, 0m));
        _attendanceRepository.Setup(r => r.UpdateAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().Handle(Command(UtcAt(17)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.Checkout);
    }

    // ── OT ghi nhận tự động khi quét tan ca muộn hơn giờ kết thúc ca chính ──────────

    private static Domain.Entities.Attendance.OvertimeRequest ApprovedOt(TimeOnly start, TimeOnly end) => new()
    {
        Id = 900,
        EmployeeInformationId = EmployeeInformationId,
        WorkDate = Today,
        OtStartTime = start,
        OtEndTime = end,
        Status = OtRequestStatus.Approved,
    };

    /// <summary>Set up đầy đủ mock cho nhánh checkout (LaborLaw + splitter + UpdateAsync) — dùng chung cho các test OT.</summary>
    private void ArrangeCheckoutDependencies()
    {
        _complianceLookup.Setup(c => c.GetLaborLawConstantsAtAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Application.Common.Models.Compliance.LaborLawConstantsSnapshot(
                VersionId: 1, ValidFrom: Today,
                ProbationSalaryMinPercent: 0m, OvertimeNightExtraPercent: 0m, NightWorkSurchargePercent: 0m,
                OvertimeNormalUpliftPercent: 0m, NightStartTime: new TimeOnly(22, 0), NightEndTime: new TimeOnly(6, 0),
                StandardWorkingHoursPerDay: 8m, MaxRegularHoursPerWeek: 48m, StandardWorkingDaysPerMonth: 26m,
                AnnualLeaveMinDays: 12, WorkRateNormalFloor: 1m, WorkRateWeeklyRestFloor: 1m, WorkRateHolidayFloor: 1m,
                OvertimeMaxHoursPerDay: 4m, OvertimeMaxHoursPerMonth: 40m, OvertimeMaxHoursPerYear: 200, LegalReference: "test"));
        _splitter.Setup(s => s.Split(It.IsAny<WorkHourDateTimeSplitInput>()))
            .Returns(new WorkHourBreakdown(1m, 0m));
        _attendanceRepository.Setup(r => r.UpdateAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private (ShiftTemplate Shift, AttendanceRecord Existing) ArrangeOpenShift()
    {
        var shift = DayShift();
        var checkInAt = TimeZoneInfo.ConvertTimeFromUtc(UtcAt(8), VnZone);
        var existing = new AttendanceRecord
        {
            Id = 55,
            EmployeeInformationId = EmployeeInformationId,
            WorkDate = Today,
            ShiftTemplateId = shift.Id,
            ShiftTemplate = shift,
            CheckInAt = checkInAt,
            CheckOutAt = null,
        };
        _shiftAssignmentRepository.Setup(r => r.GetByEmployeeAndDateAsync(
                EmployeeInformationId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssignmentFor(shift, Today)]);
        _attendanceRepository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, Today, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        return (shift, existing);
    }

    [Fact]
    public async Task Handle_RecordsActualOvertime_WhenCheckoutIsAfterShiftEndAndApprovedOtExists()
    {
        ArrangeCommon();
        ArrangeOpenShift(); // ca 8h-17h
        ArrangeCheckoutDependencies();
        _overtimeRepository.Setup(r => r.GetActiveOnDateForEmployeeAsync(
                EmployeeInformationId, Today, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ApprovedOt(new TimeOnly(17, 0), new TimeOnly(20, 30))]);
        _attendanceRepository.Setup(r => r.GetByOvertimeRequestIdAsync(900, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        _attendanceRepository.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(777);

        // Quét tan ca thật lúc 20:00 — trong khung đã duyệt (17h-20h30).
        var result = await CreateSut().Handle(Command(UtcAt(20)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.Checkout);
        _attendanceRepository.Verify(r => r.AddAsync(
            It.Is<AttendanceRecord>(a =>
                a.OvertimeRequestId == 900 &&
                a.ShiftTemplateId == null &&
                a.CheckInAt!.Value.Hour == 17 &&
                a.CheckOutAt!.Value.Hour == 20),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CapsOvertimeToApprovedEndTime_WhenActualCheckoutExceedsApprovedWindow()
    {
        ArrangeCommon();
        ArrangeOpenShift(); // ca 8h-17h
        ArrangeCheckoutDependencies();
        // Đơn duyệt chỉ tới 20h nhưng quét tan ca thật lúc 21h (làm dư 1 tiếng ngoài phê duyệt).
        _overtimeRepository.Setup(r => r.GetActiveOnDateForEmployeeAsync(
                EmployeeInformationId, Today, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ApprovedOt(new TimeOnly(17, 0), new TimeOnly(20, 0))]);
        _attendanceRepository.Setup(r => r.GetByOvertimeRequestIdAsync(900, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        _attendanceRepository.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(777);

        var result = await CreateSut().Handle(Command(UtcAt(21)), CancellationToken.None);

        result.Success.Should().BeTrue();
        _attendanceRepository.Verify(r => r.AddAsync(
            It.Is<AttendanceRecord>(a => a.CheckOutAt!.Value.Hour == 20 && a.CheckOutAt!.Value.Minute == 0),
            It.IsAny<CancellationToken>()), Times.Once,
            "quét trễ hơn giờ đã duyệt (20h) không được ghi OT vượt quá khung đã phê duyệt");
    }

    [Fact]
    public async Task Handle_DoesNotRecordOvertime_WhenNoApprovedOtExistsForThatDay()
    {
        ArrangeCommon();
        ArrangeOpenShift(); // ca 8h-17h
        ArrangeCheckoutDependencies();
        // ArrangeCommon() đã mặc định set GetActiveOnDateForEmployeeAsync trả về rỗng.

        var result = await CreateSut().Handle(Command(UtcAt(20)), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CheckinType.Should().Be(FaceCheckinType.Checkout);
        _attendanceRepository.Verify(r => r.AddAsync(
            It.Is<AttendanceRecord>(a => a.OvertimeRequestId != null), It.IsAny<CancellationToken>()), Times.Never,
            "không có đơn OT được duyệt thì không được tự suy diễn ra bản ghi OT nào");
    }

    [Fact]
    public async Task Handle_UpdatesExistingOvertimeRecord_WhenAlreadyRecorded()
    {
        ArrangeCommon();
        ArrangeOpenShift();
        ArrangeCheckoutDependencies();
        _overtimeRepository.Setup(r => r.GetActiveOnDateForEmployeeAsync(
                EmployeeInformationId, Today, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ApprovedOt(new TimeOnly(17, 0), new TimeOnly(20, 30))]);
        var existingOtRecord = new AttendanceRecord
        {
            Id = 88,
            EmployeeInformationId = EmployeeInformationId,
            WorkDate = Today,
            OvertimeRequestId = 900,
            ShiftTemplateId = null,
        };
        _attendanceRepository.Setup(r => r.GetByOvertimeRequestIdAsync(900, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOtRecord);

        var result = await CreateSut().Handle(Command(UtcAt(20)), CancellationToken.None);

        result.Success.Should().BeTrue();
        _attendanceRepository.Verify(r => r.AddAsync(
            It.Is<AttendanceRecord>(a => a.OvertimeRequestId == 900), It.IsAny<CancellationToken>()), Times.Never);
        _attendanceRepository.Verify(r => r.UpdateAsync(
            It.Is<AttendanceRecord>(a => a.Id == 88), It.IsAny<CancellationToken>()), Times.Once);
    }
}
