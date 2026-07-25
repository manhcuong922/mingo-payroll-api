using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.BackgroundJobs;
using Xunit;

namespace MPS.Application.Tests.Features.ContractTermination;

/// <summary>
/// Case 7 — lõi job vô hiệu hoá tài khoản NV có HĐ đã chấm dứt tới hạn. Test phần thuần (không DI):
/// lấy NV tới hạn → uỷ quyền cho DeactivateForEmployeesWithoutActiveContractAsync (tự lọc "còn HĐ Active").
/// </summary>
public sealed class TerminationAccountDeactivationJobTests
{
    private readonly Mock<IContractTerminationRepository> _terminations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly DateTime _now = new(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Deactivates_MaturedTerminatedEmployees_AndReturnsCount()
    {
        var maturedIds = new[] { 5, 6 };
        _terminations
            .Setup(t => t.GetEmployeeInformationIdsWithMaturedTerminationAsync(_now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(maturedIds);
        _users
            .Setup(u => u.DeactivateForEmployeesWithoutActiveContractAsync(maturedIds, _now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await TerminationAccountDeactivationJob.DeactivateMaturedAsync(
            _terminations.Object, _users.Object, _now, Mock.Of<ILogger>(), CancellationToken.None);

        result.Should().Be(2);
        _users.Verify(
            u => u.DeactivateForEmployeesWithoutActiveContractAsync(maturedIds, _now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoMaturedTerminations_ReturnsZero_AndSkipsDeactivation()
    {
        _terminations
            .Setup(t => t.GetEmployeeInformationIdsWithMaturedTerminationAsync(_now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        var result = await TerminationAccountDeactivationJob.DeactivateMaturedAsync(
            _terminations.Object, _users.Object, _now, Mock.Of<ILogger>(), CancellationToken.None);

        result.Should().Be(0);
        _users.Verify(
            u => u.DeactivateForEmployeesWithoutActiveContractAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
