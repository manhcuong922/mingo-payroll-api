using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;
using Xunit;
using AttendanceRecordEntity = MPS.Domain.Entities.Attendance.AttendanceRecord;
using LateEarlyRequestEntity = MPS.Domain.Entities.Attendance.LateEarlyRequest;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// LateEarlyAttendanceDock giờ RECOMPUTE giờ công từ nguồn (CheckIn/Out + shift + tổng phút miễn)
/// thay vì trừ trực tiếp — đơn Approved cộng trả phần giờ đã mất do trễ/sớm.
/// </summary>
public sealed class LateEarlyAttendanceDockTests
{
    private const int EmpId = 5;
    private const int RequestId = 77;
    private const int ApproverUserId = 2;
    private const int ShiftId = 1;
    private static readonly DateOnly WorkDate = new(2026, 7, 20);
    private static readonly DateTime NowUtc = new(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAttendanceRecordRepository> _records = new();
    private readonly Mock<IShiftTemplateRepository> _shifts = new();
    private readonly Mock<IComplianceLookupService> _compliance = new();
    private readonly Mock<IWorkHourSplitter> _splitter = new();
    private readonly Mock<ILateEarlyRequestRepository> _lateEarly = new();

    private LateEarlyAttendanceDock Sut()
    {
        var resolver = new LateEarlyExcuseResolver(_lateEarly.Object);
        return new(
            _records.Object, _shifts.Object, _compliance.Object,
            _splitter.Object, resolver, Mock.Of<ILogger<LateEarlyAttendanceDock>>());
    }

    private static ShiftTemplate Shift() => new()
    {
        Id = ShiftId,
        Code = "MORNING",
        Name = "Ca sáng",
        StartTime = new TimeOnly(8, 0),
        EndTime = new TimeOnly(17, 0),
        BreakStartTime = new TimeOnly(12, 0),
        BreakEndTime = new TimeOnly(13, 0),
        IsActive = true,
    };

    private static AttendanceRecordEntity MainShiftLate30() => new()
    {
        Id = 100,
        EmployeeInformationId = EmpId,
        WorkDate = WorkDate,
        ShiftTemplateId = ShiftId,
        ShiftTemplate = Shift(),
        OvertimeRequestId = null,
        // Đi trễ 30': check-in 8:30, out 17:00 → calc trước đó chỉ ghi được 7.5h.
        CheckInAt = WorkDate.ToDateTime(new TimeOnly(8, 30)),
        CheckOutAt = WorkDate.ToDateTime(new TimeOnly(17, 0)),
        Status = AttendanceStatus.Late,
        DayHours = 7.5m,
        NightHours = 0m,
    };

    private void RecordsReturn(params AttendanceRecordEntity[] recs) =>
        _records.Setup(r => r.GetByEmployeeAndDateAsync(EmpId, WorkDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recs);

    private void ApprovedRequests(params LateEarlyRequestEntity[] items) =>
        _lateEarly.Setup(r => r.GetApprovedByEmployeeAndDateAsync(EmpId, WorkDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

    private void SplitterReturns(decimal dayHours, decimal nightHours = 0m) =>
        _splitter.Setup(s => s.Split(It.IsAny<WorkHourDateTimeSplitInput>()))
            .Returns(new WorkHourBreakdown(dayHours, nightHours));

    private void LaborLawStandard() =>
        _compliance.Setup(c => c.GetLaborLawConstantsAtAsync(WorkDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LaborLawConstantsSnapshot(
                VersionId: 1, ValidFrom: WorkDate,
                ProbationSalaryMinPercent: 0m, OvertimeNightExtraPercent: 0m,
                NightWorkSurchargePercent: 0m, OvertimeNormalUpliftPercent: 0m,
                NightStartTime: new TimeOnly(22, 0), NightEndTime: new TimeOnly(6, 0),
                StandardWorkingHoursPerDay: 8m, MaxRegularHoursPerWeek: 48m,
                StandardWorkingDaysPerMonth: 26m, AnnualLeaveMinDays: 12,
                WorkRateNormalFloor: 1m, WorkRateWeeklyRestFloor: 1m, WorkRateHolidayFloor: 1m,
                OvertimeMaxHoursPerDay: 4m, OvertimeMaxHoursPerMonth: 40m,
                OvertimeMaxHoursPerYear: 200, LegalReference: "test"));

    [Fact]
    public async Task Recompute_RestoresHours_AndLinksRequest_WhenLateApproved()
    {
        var target = MainShiftLate30();
        RecordsReturn(target);
        ApprovedRequests(new LateEarlyRequestEntity
        {
            Id = RequestId, EmployeeInformationId = EmpId, WorkDate = WorkDate,
            Kind = LateEarlyKind.LateArrival, Minutes = 30,
            Status = LateEarlyRequestStatus.Approved,
        });
        LaborLawStandard();
        // Với excused 30' → cửa sổ hiệu dụng dời về 8:00-17:00 → splitter được gọi trả 8h công.
        SplitterReturns(dayHours: 8m);

        await Sut().RecomputeAsync(EmpId, WorkDate, RequestId, ApproverUserId, NowUtc, default);

        target.DayHours.Should().Be(8m);
        target.LateEarlyRequestId.Should().Be(RequestId);
        _records.Verify(r => r.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Recompute_IsIdempotent_WhenCalledTwice()
    {
        var target = MainShiftLate30();
        RecordsReturn(target);
        ApprovedRequests(new LateEarlyRequestEntity
        {
            Id = RequestId, EmployeeInformationId = EmpId, WorkDate = WorkDate,
            Kind = LateEarlyKind.LateArrival, Minutes = 30,
            Status = LateEarlyRequestStatus.Approved,
        });
        LaborLawStandard();
        SplitterReturns(dayHours: 8m);

        await Sut().RecomputeAsync(EmpId, WorkDate, RequestId, ApproverUserId, NowUtc, default);
        await Sut().RecomputeAsync(EmpId, WorkDate, RequestId, ApproverUserId, NowUtc, default);

        // Gọi 2 lần vẫn = 8h (tính lại từ nguồn, không cộng dồn).
        target.DayHours.Should().Be(8m);
    }

    [Fact]
    public async Task NoMainShiftRecord_SkipsSilently()
    {
        RecordsReturn(); // chưa có bản ghi chấm công

        await Sut().RecomputeAsync(EmpId, WorkDate, RequestId, ApproverUserId, NowUtc, default);

        _records.Verify(r => r.UpdateAsync(It.IsAny<AttendanceRecordEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _splitter.Verify(s => s.Split(It.IsAny<WorkHourDateTimeSplitInput>()), Times.Never);
    }
}
