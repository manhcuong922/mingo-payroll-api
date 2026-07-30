using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Domain.Interfaces.Attendance;
using Xunit;
using AttendanceRecordEntity = MPS.Domain.Entities.Attendance.AttendanceRecord;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// Case 2 — trừ giờ đi muộn/về sớm vào chấm công + LIÊN KẾT bản ghi với đơn (idempotent, truy vết).
/// </summary>
public sealed class LateEarlyAttendanceDockTests
{
    private const int EmpId = 5;
    private const int RequestId = 77;
    private const int ApproverUserId = 2;
    private static readonly DateOnly WorkDate = new(2026, 7, 20);
    private static readonly DateTime NowUtc = new(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAttendanceRecordRepository> _records = new();
    private LateEarlyAttendanceDock Sut() =>
        new(_records.Object, Mock.Of<ILogger<LateEarlyAttendanceDock>>());

    private void Records(params AttendanceRecordEntity[] recs) =>
        _records.Setup(r => r.GetByEmployeeAndDateAsync(EmpId, WorkDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recs);

    private static AttendanceRecordEntity MainShift(decimal dayHours, int? lateEarlyRequestId = null) =>
        new() { Id = 100, ShiftTemplateId = 1, OvertimeRequestId = null, DayHours = dayHours, LateEarlyRequestId = lateEarlyRequestId };

    [Fact]
    public async Task Docks_Hours_AndLinksRequest()
    {
        var target = MainShift(dayHours: 8m);
        Records(target);

        await Sut().DockAsync(EmpId, WorkDate, minutes: 60, RequestId, ApproverUserId, NowUtc, CancellationToken.None);

        target.DayHours.Should().Be(7m);                       // trừ 1h
        target.LateEarlyRequestId.Should().Be(RequestId);      // LIÊN KẾT
        _records.Verify(r => r.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Idempotent_SkipsWhenRequestAlreadyApplied()
    {
        // Bản ghi đã mang chính đơn này → không trừ lại.
        Records(MainShift(dayHours: 7m, lateEarlyRequestId: RequestId));

        await Sut().DockAsync(EmpId, WorkDate, minutes: 60, RequestId, ApproverUserId, NowUtc, CancellationToken.None);

        _records.Verify(r => r.UpdateAsync(It.IsAny<AttendanceRecordEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoMainShiftRecord_SkipsSilently()
    {
        Records(); // chưa có bản ghi chấm công ngày đó

        await Sut().DockAsync(EmpId, WorkDate, minutes: 60, RequestId, ApproverUserId, NowUtc, CancellationToken.None);

        _records.Verify(r => r.UpdateAsync(It.IsAny<AttendanceRecordEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
