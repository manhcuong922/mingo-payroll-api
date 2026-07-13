using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Evaluation;
using MPS.Application.Features.Evaluation.OrderEvaluation.EventHandlers;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Evaluation;
using Xunit;

namespace MPS.Application.Tests.Features.Evaluation;

public class GenerateOrderEvaluationHandlerTests
{
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IOrderEvaluationRepository> _orderEvals = new();
    private readonly Mock<IEvaluationCriteriaSnapshotService> _criteria = new();

    private const int ProjectId = 1;
    private const int ResponsibleUserId = 5;

    private GenerateOrderEvaluationHandler Sut() => new(
        _projects.Object, _orderEvals.Object, _criteria.Object,
        NullLogger<GenerateOrderEvaluationHandler>.Instance);

    private static EvaluationScoreLine TemplateLine() => new()
    {
        SourceMetricId = 100, TypeNameSnapshot = "Kỷ luật", MetricNameSnapshot = "Đi làm đúng giờ",
        TypeWeightSnapshot = 100, MetricWeightSnapshot = 50, Scale = 5, Score = null,
    };

    private void SetupProject(int? responsibleUserId, DateTime? completedAt) =>
        _projects.Setup(p => p.GetByIdAsync(ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.Project
            {
                Id = ProjectId, ResponsibleUserId = responsibleUserId, CompletedAt = completedAt,
            });

    [Fact]
    public async Task Skips_WhenAlreadyExists_Idempotent_E8()
    {
        _orderEvals.Setup(r => r.ExistsForProjectAsync(ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Sut().Handle(new ProjectCompletedNotification(ProjectId), CancellationToken.None);

        _orderEvals.Verify(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Skips_WhenNoResponsibleUser_E2()
    {
        _orderEvals.Setup(r => r.ExistsForProjectAsync(ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        SetupProject(responsibleUserId: null, completedAt: new DateTime(2026, 7, 15));

        await Sut().Handle(new ProjectCompletedNotification(ProjectId), CancellationToken.None);

        _orderEvals.Verify(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Skips_WhenNoWorkers_E1()
    {
        _orderEvals.Setup(r => r.ExistsForProjectAsync(ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        SetupProject(ResponsibleUserId, new DateTime(2026, 7, 15));
        _orderEvals.Setup(r => r.GetSubjectUserIdsForProjectAsync(ProjectId, SystemRoles.EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        await Sut().Handle(new ProjectCompletedNotification(ProjectId), CancellationToken.None);

        _orderEvals.Verify(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Skips_WhenNoCriteriaConfigured_E3()
    {
        _orderEvals.Setup(r => r.ExistsForProjectAsync(ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        SetupProject(ResponsibleUserId, new DateTime(2026, 7, 15));
        _orderEvals.Setup(r => r.GetSubjectUserIdsForProjectAsync(ProjectId, SystemRoles.EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10 });
        _criteria.Setup(c => c.BuildLinesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("chưa cấu hình tiêu chí"));

        // Không ném ra ngoài (đóng đơn không được fail) và không tạo bảng.
        await Sut().Handle(new ProjectCompletedNotification(ProjectId), CancellationToken.None);

        _orderEvals.Verify(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Creates_WithSnapshotPerSubject_HappyPath()
    {
        _orderEvals.Setup(r => r.ExistsForProjectAsync(ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        SetupProject(ResponsibleUserId, new DateTime(2026, 7, 15));
        _orderEvals.Setup(r => r.GetSubjectUserIdsForProjectAsync(ProjectId, SystemRoles.EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10, 11 });
        _criteria.Setup(c => c.BuildLinesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { TemplateLine(), TemplateLine() });

        MPS.Domain.Entities.Evaluation.OrderEvaluation? captured = null;
        _orderEvals.Setup(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.Evaluation.OrderEvaluation, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(1);

        await Sut().Handle(new ProjectCompletedNotification(ProjectId), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.EvaluatorUserId.Should().Be(ResponsibleUserId);
        captured.PeriodYear.Should().Be(2026);
        captured.PeriodMonth.Should().Be(7);
        captured.Subjects.Should().HaveCount(2);
        captured.Subjects.Select(s => s.EmployeeUserId).Should().BeEquivalentTo(new[] { 10, 11 });
        captured.Subjects.Should().OnlyContain(s => s.FinalScore == null);
        captured.Subjects.Should().OnlyContain(s => s.Scores.Count == 2);
        // Snapshot riêng từng NV — không chia sẻ tham chiếu dòng điểm.
        captured.Subjects[0].Scores[0].Should().NotBeSameAs(captured.Subjects[1].Scores[0]);
    }
}
