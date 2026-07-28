using FluentAssertions;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Exceptions;
using Xunit;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// Case 4 — chặn OT ngày tương lai quá xa (>30 ngày); quá khứ và trong ngưỡng đều cho phép.
/// </summary>
public sealed class OvertimeWorkDateGuardTests
{
    private static readonly DateTime NowUtc = new(2026, 7, 20, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 7, 20);

    [Theory]
    [InlineData(-400)] // quá khứ xa (truy lĩnh [D3]) — hợp lệ
    [InlineData(-1)]   // hôm qua
    [InlineData(0)]    // hôm nay
    [InlineData(15)]   // tương lai gần
    [InlineData(30)]   // đúng ngưỡng — hợp lệ
    public void Allows_PastAndWithinHorizon(int offsetDays)
    {
        var act = () => OvertimeWorkDateGuard.EnsureNotTooFarInFuture(Today.AddDays(offsetDays), NowUtc);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(31)]  // vượt ngưỡng 1 ngày
    [InlineData(60)]
    [InlineData(400)]
    public void Blocks_TooFarInFuture(int offsetDays)
    {
        var act = () => OvertimeWorkDateGuard.EnsureNotTooFarInFuture(Today.AddDays(offsetDays), NowUtc);

        act.Should().Throw<DomainValidationException>().WithMessage("*OT_WORKDATE_TOO_FAR*");
    }
}
