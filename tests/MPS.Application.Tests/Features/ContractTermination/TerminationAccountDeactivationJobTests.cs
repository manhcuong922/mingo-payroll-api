using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.BackgroundJobs;
using Xunit;

namespace MPS.Application.Tests.Features.ContractTermination;

/// <summary>
/// Job daily làm 2 việc: (1) promote HĐ PendingTermination có TerminationDate đã qua (theo giờ VN)
/// sang Terminated; (2) deactivate account NV chấm dứt tới hạn không còn HĐ Active/PendingTermination.
/// Test phần lõi thuần (không DI).
/// </summary>
public sealed class TerminationAccountDeactivationJobTests
{
    private readonly Mock<IContractTerminationRepository> _terminations = new();
    private readonly Mock<IUserRepository> _users = new();

    // 2026-07-25 00:00 UTC = 07:00 VN cùng ngày → todayVn = 2026-07-25.
    private readonly DateTime _nowUtc = new(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _todayVn = new(2026, 7, 25);

    [Fact]
    public async Task Promotes_Matured_PendingTerminations_Then_Deactivates_Accounts()
    {
        var maturedIds = new[] { 5, 6 };
        _terminations
            .Setup(t => t.PromoteMaturedPendingTerminationsAsync(_todayVn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _terminations
            .Setup(t => t.GetEmployeeInformationIdsWithMaturedTerminationAsync(_todayVn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(maturedIds);
        _users
            .Setup(u => u.DeactivateForEmployeesWithoutActiveContractAsync(maturedIds, _nowUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await TerminationAccountDeactivationJob.RunOnceAsync(
            _terminations.Object, _users.Object, _nowUtc, Mock.Of<ILogger>(), CancellationToken.None);

        result.Should().Be(2);
        _terminations.Verify(
            t => t.PromoteMaturedPendingTerminationsAsync(_todayVn, It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(
            u => u.DeactivateForEmployeesWithoutActiveContractAsync(maturedIds, _nowUtc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task No_Matured_Terminations_Skips_Deactivation_But_Still_Runs_Promote()
    {
        _terminations
            .Setup(t => t.PromoteMaturedPendingTerminationsAsync(_todayVn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _terminations
            .Setup(t => t.GetEmployeeInformationIdsWithMaturedTerminationAsync(_todayVn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        var result = await TerminationAccountDeactivationJob.RunOnceAsync(
            _terminations.Object, _users.Object, _nowUtc, Mock.Of<ILogger>(), CancellationToken.None);

        result.Should().Be(0);
        _terminations.Verify(
            t => t.PromoteMaturedPendingTerminationsAsync(_todayVn, It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(
            u => u.DeactivateForEmployeesWithoutActiveContractAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TodayVn_Is_Computed_From_Utc_Plus_7_Hours()
    {
        // 2026-07-25 18:00 UTC = 01:00 26/07 VN → todayVn phải là 2026-07-26.
        var lateEveningUtc = new DateTime(2026, 7, 25, 18, 0, 0, DateTimeKind.Utc);
        var expectedTodayVn = new DateTime(2026, 7, 26);

        _terminations
            .Setup(t => t.PromoteMaturedPendingTerminationsAsync(expectedTodayVn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _terminations
            .Setup(t => t.GetEmployeeInformationIdsWithMaturedTerminationAsync(expectedTodayVn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        await TerminationAccountDeactivationJob.RunOnceAsync(
            _terminations.Object, _users.Object, lateEveningUtc, Mock.Of<ILogger>(), CancellationToken.None);

        _terminations.Verify(
            t => t.PromoteMaturedPendingTerminationsAsync(expectedTodayVn, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
