using FluentAssertions;
using Moq;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using Xunit;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// Cap 8h/NGÀY khi phân ca phải CHẶN CỨNG (Điều 105 k.1 BLLĐ 2019).
///
/// <para><b>Lỗi gốc</b>: một điều kiện <c>if (!request.AcknowledgeWeeklyOvertimeWarnings &amp;&amp; (daily || weekly))</c>
/// gộp chung cả hai cap, nên cờ acknowledge — vốn sinh ra cho popup xác nhận cap 48h/TUẦN — bỏ qua
/// luôn cả cap ngày. Người phân ca chỉ cần bấm "Có, vẫn phân" là lưu được 16h/ngày dưới dạng giờ làm
/// việc BÌNH THƯỜNG: giờ đó không đi qua đơn tăng ca nên vừa không có trần 12h/ngày, vừa không được
/// trả theo hệ số OT.</para>
///
/// <para>Cap 48h/tuần vẫn giữ warn-then-confirm — tuần 48h có thể hợp lệ khi bố trí lại ngày nghỉ.</para>
/// </summary>
public sealed class ShiftDailyCapHardBlockTests
{
    private const int EmployeeId = 7;
    private const int ShiftId = 3;
    private static readonly DateOnly WorkDate = new(2026, 10, 1);

    private readonly Mock<IEmployeeShiftAssignmentRepository> _assignments = new();
    private readonly Mock<IShiftTemplateRepository> _shifts = new();
    private readonly Mock<IEmployeeInformationRepository> _employees = new();
    private readonly Mock<IRegularHoursCapChecker> _capChecker = new();
    private readonly Mock<IDayTypeResolver> _dayTypes = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IClock> _clock = new();

    public ShiftDailyCapHardBlockTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(1);
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc));

        _employees
            .Setup(e => e.GetEmployeeInformationIdsWithActiveUserAsync(
                It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { EmployeeId });

        _assignments
            .Setup(a => a.GetByEmployeesAndDateRangeAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeShiftAssignment>());

        _assignments
            .Setup(a => a.GetLockedShiftsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(int, DateOnly, int)>());

        _shifts
            .Setup(s => s.GetByIdAsync(ShiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShiftTemplate
            {
                Id = ShiftId,
                Code = "FULLTIME",
                Name = "Ca hành chính",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 0),
                StandardHours = 8m,
                IsActive = true,
            });

        _dayTypes
            .Setup(d => d.ResolveRangeAsync(
                It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateOnly, AttendanceEnums.DayType>
            {
                [WorkDate] = AttendanceEnums.DayType.Normal,
            });

        // Mặc định: không vượt cap nào.
        _capChecker
            .Setup(c => c.CheckDailyAssignmentsAsync(It.IsAny<IEnumerable<WeeklyCapDelta>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DailyCapWarning>());
        _capChecker
            .Setup(c => c.CheckAssignmentsAsync(It.IsAny<IEnumerable<WeeklyCapDelta>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WeeklyCapWarning>());
    }

    private BatchUpdateShiftAssignmentsCommandHandler CreateSut() => new(
        _assignments.Object,
        _shifts.Object,
        _employees.Object,
        _capChecker.Object,
        _dayTypes.Object,
        _currentUser.Object,
        _clock.Object);

    private static BatchUpdateShiftAssignmentsCommand Command(bool acknowledge) => new(
        new[] { new BatchShiftOperation(BatchShiftOperationKind.Add, EmployeeId, WorkDate, ShiftId) },
        BatchShiftConflictPolicy.Skip,
        SourceScheduleTemplateId: null,
        AcknowledgeWeeklyOvertimeWarnings: acknowledge,
        IncludeRestDaysAndHolidays: true);

    private void GivenDailyCapExceeded() =>
        _capChecker
            .Setup(c => c.CheckDailyAssignmentsAsync(It.IsAny<IEnumerable<WeeklyCapDelta>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DailyCapWarning(EmployeeId, WorkDate, TotalHoursAfter: 16m, CapHours: 8m) });

    private void GivenWeeklyCapExceeded() =>
        _capChecker
            .Setup(c => c.CheckAssignmentsAsync(It.IsAny<IEnumerable<WeeklyCapDelta>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyCapWarning(EmployeeId, IsoYear: 2026, IsoWeek: 40,
                    WeekStart: new DateOnly(2026, 9, 28), WeekEnd: new DateOnly(2026, 10, 4),
                    TotalHoursAfter: 56m, CapHours: 48m),
            });

    [Fact(DisplayName = "Vượt 8h/ngày → chặn kể cả khi người dùng đã xác nhận")]
    public async Task DailyCapExceeded_IsBlocked_EvenWhenAcknowledged()
    {
        GivenDailyCapExceeded();

        var act = () => CreateSut().Handle(Command(acknowledge: true), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RegularHoursCapExceededException>();
        ex.Which.DailyViolations.Should().ContainSingle()
            .Which.TotalHoursAfter.Should().Be(16m);
        _assignments.Verify(a => a.ApplyBatchAsync(
            It.IsAny<IReadOnlyCollection<EmployeeShiftAssignment>>(),
            It.IsAny<IReadOnlyCollection<(int, int, int?)>>(),
            It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Vượt 8h/ngày → chặn khi chưa xác nhận")]
    public async Task DailyCapExceeded_IsBlocked_WhenNotAcknowledged()
    {
        GivenDailyCapExceeded();

        var act = () => CreateSut().Handle(Command(acknowledge: false), CancellationToken.None);

        await act.Should().ThrowAsync<RegularHoursCapExceededException>();
    }

    [Fact(DisplayName = "Chỉ vượt 48h/tuần → xác nhận xong vẫn phân được (warn-then-confirm)")]
    public async Task WeeklyCapOnly_IsAllowed_WhenAcknowledged()
    {
        GivenWeeklyCapExceeded();

        var result = await CreateSut().Handle(Command(acknowledge: true), CancellationToken.None);

        result.Applied.Should().Be(1);
        _assignments.Verify(a => a.ApplyBatchAsync(
            It.IsAny<IReadOnlyCollection<EmployeeShiftAssignment>>(),
            It.IsAny<IReadOnlyCollection<(int, int, int?)>>(),
            It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Chỉ vượt 48h/tuần, chưa xác nhận → chặn, và KHÔNG kèm vi phạm ngày")]
    public async Task WeeklyCapOnly_IsBlocked_WhenNotAcknowledged()
    {
        GivenWeeklyCapExceeded();

        var act = () => CreateSut().Handle(Command(acknowledge: false), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RegularHoursCapExceededException>();
        ex.Which.WeeklyViolations.Should().ContainSingle();
        ex.Which.DailyViolations.Should().BeEmpty();
    }

    [Fact(DisplayName = "Không vượt cap nào → phân ca bình thường")]
    public async Task NoCapExceeded_AppliesNormally()
    {
        var result = await CreateSut().Handle(Command(acknowledge: false), CancellationToken.None);

        result.Applied.Should().Be(1);
    }
}
