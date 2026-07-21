using FluentAssertions;
using Moq;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// BS-3 / [D8] — mỗi ngày chỉ được 1 đơn Leave/WFH/LateEarly. Trước bản vá guard chỉ tồn tại lẻ tẻ
/// theo từng loại (WFH↔WFH, WFH↔Nghỉ-Approved, LateEarly cùng kind) nên vẫn tạo được đơn chồng chéo.
/// </summary>
public sealed class RequestDayGuardTests
{
    private const int EmployeeId = 42;
    private static readonly DateOnly Day = new(2026, 7, 15);

    private readonly Mock<IRequestDayOccupancyReader> _reader = new();

    [Fact]
    public async Task EnsureDayFree_Passes_WhenNoRequestOccupiesTheRange()
    {
        _reader.Setup(r => r.FindFirstOccupiedAsync(
                EmployeeId, Day, Day, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OccupiedDay?)null);

        var act = () => RequestDayGuard.EnsureDayFreeAsync(_reader.Object, EmployeeId, Day, Day);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(RequestDayKind.Leave, "nghỉ phép")]
    [InlineData(RequestDayKind.Wfh, "WFH")]
    [InlineData(RequestDayKind.LateEarly, "đi muộn/về sớm")]
    public async Task EnsureDayFree_Throws_NamingTheConflictingRequestKind(RequestDayKind kind, string label)
    {
        _reader.Setup(r => r.FindFirstOccupiedAsync(
                EmployeeId, Day, Day, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OccupiedDay(Day, kind));

        var act = () => RequestDayGuard.EnsureDayFreeAsync(_reader.Object, EmployeeId, Day, Day);

        var thrown = await act.Should().ThrowAsync<DomainValidationException>();
        thrown.WithMessage($"*đơn {label}*");
        thrown.WithMessage("*15/07/2026*");
        // Thông báo hợp nhất mà các spec E2E bám theo.
        thrown.WithMessage("*Mỗi ngày chỉ được có 1 đơn Leave/WFH/LateEarly*");
    }

    [Fact]
    public async Task EnsureNotOnApprovedLeave_Throws_WhenOvertimeFallsOnApprovedLeaveDay()
    {
        _reader.Setup(r => r.FindFirstApprovedLeaveDayAsync(
                EmployeeId, Day, Day, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Day);

        var act = () => RequestDayGuard.EnsureNotOnApprovedLeaveAsync(_reader.Object, EmployeeId, Day, Day);

        (await act.Should().ThrowAsync<DomainValidationException>())
            .WithMessage("*nghỉ phép*không thể đăng ký làm thêm giờ*");
    }

    [Fact]
    public async Task EnsureNotOnApprovedLeave_Passes_WhenOnlyWfhOrLateEarlyExists()
    {
        // OT vẫn hợp lệ trong ngày WFH / đi muộn — chỉ nghỉ phép mới loại trừ OT.
        _reader.Setup(r => r.FindFirstApprovedLeaveDayAsync(
                EmployeeId, Day, Day, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly?)null);

        var act = () => RequestDayGuard.EnsureNotOnApprovedLeaveAsync(_reader.Object, EmployeeId, Day, Day);

        await act.Should().NotThrowAsync();
    }
}
