using FluentAssertions;
using Moq;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Interfaces.Evaluation;
using Xunit;

namespace MPS.Application.Tests.Features.Evaluation;

public class MonthlyAggregationServiceTests
{
    private readonly Mock<IOrderEvaluationRepository> _orderEvals = new();
    private readonly Mock<IMonthlyEvaluationRepository> _monthly = new();

    private const int Year = 2026;
    private const int Month = 7;
    private const int ActorId = 2;

    private MonthlyAggregationService Sut() => new(_orderEvals.Object, _monthly.Object);

    private static OrderEvaluationSubject Subject(int id, int employeeUserId, decimal finalScore) => new()
    {
        Id = id, EmployeeUserId = employeeUserId, FinalScore = finalScore, SubjectRoleIdSnapshot = SystemRoles.EmployeeId,
    };

    [Fact]
    public async Task Averages_MultipleOrders_ForOneEmployee_D7()
    {
        _orderEvals.Setup(r => r.GetSubmittedSubjectsByMonthAsync(Year, Month, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Subject(1, 10, 4.0m), Subject(2, 10, 2.0m), Subject(3, 10, 3.0m) });

        decimal? capturedScore = null;
        IReadOnlyList<AggregatedContribution>? capturedContribs = null;
        _monthly.Setup(r => r.UpsertAggregatedAsync(10, Year, Month, SystemRoles.EmployeeId, ActorId,
                It.IsAny<decimal>(), It.IsAny<IReadOnlyList<AggregatedContribution>>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, int, int, int, decimal, IReadOnlyList<AggregatedContribution>, CancellationToken>(
                (_, _, _, _, _, score, contribs, _) => { capturedScore = score; capturedContribs = contribs; })
            .ReturnsAsync(MonthlyAggregateOutcome.Created);

        var updated = await Sut().AggregateAsync(Year, Month, null, ActorId, CancellationToken.None);

        updated.Should().Be(1);
        capturedScore.Should().Be(3.00m);                 // (4+2+3)/3
        capturedContribs.Should().HaveCount(3);
    }

    [Fact]
    public async Task AggregatesEachEmployeeSeparately_E9()
    {
        _orderEvals.Setup(r => r.GetSubmittedSubjectsByMonthAsync(Year, Month, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Subject(1, 10, 4.0m), Subject(2, 10, 2.0m), Subject(3, 11, 5.0m) });
        _monthly.Setup(r => r.UpsertAggregatedAsync(It.IsAny<int>(), Year, Month, It.IsAny<int>(), ActorId,
                It.IsAny<decimal>(), It.IsAny<IReadOnlyList<AggregatedContribution>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MonthlyAggregateOutcome.Created);

        var updated = await Sut().AggregateAsync(Year, Month, null, ActorId, CancellationToken.None);

        updated.Should().Be(2);
        _monthly.Verify(r => r.UpsertAggregatedAsync(10, Year, Month, SystemRoles.EmployeeId, ActorId, 3.00m,
            It.IsAny<IReadOnlyList<AggregatedContribution>>(), It.IsAny<CancellationToken>()), Times.Once);
        _monthly.Verify(r => r.UpsertAggregatedAsync(11, Year, Month, SystemRoles.EmployeeId, ActorId, 5.00m,
            It.IsAny<IReadOnlyList<AggregatedContribution>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoesNotCount_ApprovedBoards_E12()
    {
        _orderEvals.Setup(r => r.GetSubmittedSubjectsByMonthAsync(Year, Month, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Subject(1, 10, 4.0m) });
        _monthly.Setup(r => r.UpsertAggregatedAsync(10, Year, Month, SystemRoles.EmployeeId, ActorId,
                It.IsAny<decimal>(), It.IsAny<IReadOnlyList<AggregatedContribution>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MonthlyAggregateOutcome.SkippedApproved);

        var updated = await Sut().AggregateAsync(Year, Month, null, ActorId, CancellationToken.None);

        updated.Should().Be(0);   // bảng đã khóa → bỏ qua, không đếm
    }

    [Fact]
    public async Task ReturnsZero_WhenNoSubmittedSubjects()
    {
        _orderEvals.Setup(r => r.GetSubmittedSubjectsByMonthAsync(Year, Month, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OrderEvaluationSubject>());

        var updated = await Sut().AggregateAsync(Year, Month, null, ActorId, CancellationToken.None);

        updated.Should().Be(0);
        _monthly.Verify(r => r.UpsertAggregatedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<AggregatedContribution>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
