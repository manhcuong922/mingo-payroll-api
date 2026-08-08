using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Evaluation;
using Xunit;

namespace MPS.Application.Tests.Features.Evaluation;

public class DirectEvaluationAutoGenerateServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IMonthlyEvaluationRepository> _monthly = new();
    private readonly Mock<IOrderEvaluationRepository> _orderEvals = new();
    private readonly Mock<IEvaluationCriteriaSnapshotService> _criteria = new();

    /// <summary>Các bảng SUT gửi xuống repo — điền bởi callback của <c>AddRangeAsync</c>.</summary>
    private IReadOnlyList<MPS.Domain.Entities.Evaluation.MonthlyEvaluation> _added =
        Array.Empty<MPS.Domain.Entities.Evaluation.MonthlyEvaluation>();

    private const int Year = 2026;
    private const int Month = 7;
    private const int ActorId = 1;

    public DirectEvaluationAutoGenerateServiceTests()
    {
        // Mặc định: chưa ai có bảng tháng, chưa ai dính bảng-theo-đơn, rubric cân 100%.
        _monthly.Setup(r => r.GetExistingEmployeeUserIdsForMonthAsync(Year, Month, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());
        _orderEvals.Setup(r => r.GetSubjectUserIdsByMonthAsync(Year, Month, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());
        _criteria.Setup(c => c.BuildLinesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BalancedRubric());
        _monthly.Setup(r => r.AddRangeAsync(
                It.IsAny<IReadOnlyList<MPS.Domain.Entities.Evaluation.MonthlyEvaluation>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<MPS.Domain.Entities.Evaluation.MonthlyEvaluation>, CancellationToken>(
                (e, _) => _added = e)
            .ReturnsAsync((IReadOnlyList<MPS.Domain.Entities.Evaluation.MonthlyEvaluation> e, CancellationToken _) => e.Count);
    }

    private DirectEvaluationAutoGenerateService Sut() => new(
        _users.Object, _monthly.Object, _orderEvals.Object, _criteria.Object,
        NullLogger<DirectEvaluationAutoGenerateService>.Instance);

    private static MPS.Domain.Entities.User Person(int id, int roleId) => new() { Id = id, RoleId = roleId, Status = 1 };

    /// <summary>Rubric cân: 2 nhóm 60/40, mỗi nhóm 2 tiêu chí 50/50.</summary>
    private static IReadOnlyList<EvaluationScoreLine> BalancedRubric() => new[]
    {
        Line("Kết quả", 60, "Khối lượng", 50),
        Line("Kết quả", 60, "Chất lượng", 50),
        Line("Kỷ luật", 40, "Nội quy", 50),
        Line("Kỷ luật", 40, "Trách nhiệm", 50),
    };

    private static EvaluationScoreLine Line(string type, decimal typeWeight, string metric, decimal metricWeight) => new()
    {
        TypeNameSnapshot = type,
        TypeWeightSnapshot = typeWeight,
        MetricNameSnapshot = metric,
        MetricWeightSnapshot = metricWeight,
        Scale = 5,
        Score = null,
    };

    private void SetupCandidates(params MPS.Domain.Entities.User[] people) =>
        _users.Setup(r => r.GetActiveByRoleIdsAsync(
                EvaluationSubjectRoles.DirectScorableRoleIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(people);

    [Fact]
    public async Task Creates_DefaultBoard_ForEachDirectScorableRole()
    {
        SetupCandidates(
            Person(10, SystemRoles.ProductionTeamLeaderId),
            Person(11, SystemRoles.PlanningManagerId),
            Person(12, SystemRoles.AccountantId),
            Person(13, SystemRoles.HrId));

        var result = await Sut().GenerateForMonthAsync(Year, Month, ActorId, CancellationToken.None);

        result.Created.Should().Be(4);
        result.Candidates.Should().Be(4);
        result.SkippedExisting.Should().Be(0);
        result.SkippedOrderScored.Should().Be(0);
        result.RubricError.Should().BeNull();
    }

    [Fact]
    public async Task DefaultScore_Is4_OnEveryLine_AndFinalScoreIs4()
    {
        SetupCandidates(Person(10, SystemRoles.ProductionTeamLeaderId));
        await Sut().GenerateForMonthAsync(Year, Month, ActorId, CancellationToken.None);

        var board = _added.Single();
        board.Scores.Should().OnlyContain(l => l.Score == EvaluationSubjectRoles.DefaultDirectScore);
        board.FinalScore.Should().Be(4.00m);
        board.IsSystemDefault.Should().BeTrue();
        board.Source.Should().Be(EvaluationSource.Direct);
        board.Status.Should().Be(EvaluationStatus.Draft);
        board.SubjectRoleIdSnapshot.Should().Be(SystemRoles.ProductionTeamLeaderId);
        board.CreatedByUserId.Should().Be(ActorId);
    }

    [Fact]
    public async Task EachBoard_GetsItsOwnScoreLineSnapshot_D2()
    {
        SetupCandidates(
            Person(10, SystemRoles.ProductionTeamLeaderId),
            Person(11, SystemRoles.HrId));
        await Sut().GenerateForMonthAsync(Year, Month, ActorId, CancellationToken.None);

        var boards = _added;
        boards.Should().HaveCount(2);
        boards[0].Scores.Should().NotBeSameAs(boards[1].Scores);
        boards[0].Scores[0].Should().NotBeSameAs(boards[1].Scores[0]);
    }

    [Fact]
    public async Task Skips_PeopleWhoAlreadyHaveAMonthlyBoard_Idempotent()
    {
        SetupCandidates(
            Person(10, SystemRoles.ProductionTeamLeaderId),
            Person(11, SystemRoles.HrId));
        _monthly.Setup(r => r.GetExistingEmployeeUserIdsForMonthAsync(Year, Month, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10 });
        var result = await Sut().GenerateForMonthAsync(Year, Month, ActorId, CancellationToken.None);

        result.Created.Should().Be(1);
        result.SkippedExisting.Should().Be(1);
        _added.Single().EmployeeUserId.Should().Be(11);
    }

    [Fact]
    public async Task Skips_PeopleStillScoredByOrder_WhenPositionChangedMidPeriod()
    {
        // NV 10 vừa lên tổ trưởng giữa kỳ: kỳ này còn bảng-theo-đơn ⇒ điểm tháng gộp từ đơn,
        // chấm trực tiếp áp dụng từ kỳ sau.
        SetupCandidates(
            Person(10, SystemRoles.ProductionTeamLeaderId),
            Person(11, SystemRoles.HrId));
        _orderEvals.Setup(r => r.GetSubjectUserIdsByMonthAsync(Year, Month, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10 });
        var result = await Sut().GenerateForMonthAsync(Year, Month, ActorId, CancellationToken.None);

        result.Created.Should().Be(1);
        result.SkippedOrderScored.Should().Be(1);
        _added.Single().EmployeeUserId.Should().Be(11);
    }

    [Fact]
    public async Task CreatesNothing_AndReportsError_WhenRubricNotReady_E3()
    {
        SetupCandidates(Person(10, SystemRoles.HrId));
        _criteria.Setup(c => c.BuildLinesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Rubric chưa cân: tổng trọng số các nhóm đang là 90%."));

        var result = await Sut().GenerateForMonthAsync(Year, Month, ActorId, CancellationToken.None);

        result.Created.Should().Be(0);
        result.RubricError.Should().Contain("chưa cân");
        _monthly.Verify(r => r.AddRangeAsync(
            It.IsAny<IReadOnlyList<MPS.Domain.Entities.Evaluation.MonthlyEvaluation>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DoesNotTouchRubric_WhenNoCandidates()
    {
        SetupCandidates();

        var result = await Sut().GenerateForMonthAsync(Year, Month, ActorId, CancellationToken.None);

        result.Created.Should().Be(0);
        result.Candidates.Should().Be(0);
        _criteria.Verify(c => c.BuildLinesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DoesNotTouchRubric_WhenEveryCandidateIsSkipped()
    {
        SetupCandidates(Person(10, SystemRoles.HrId));
        _monthly.Setup(r => r.GetExistingEmployeeUserIdsForMonthAsync(Year, Month, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10 });

        var result = await Sut().GenerateForMonthAsync(Year, Month, ActorId, CancellationToken.None);

        result.Created.Should().Be(0);
        result.SkippedExisting.Should().Be(1);
        _criteria.Verify(c => c.BuildLinesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
