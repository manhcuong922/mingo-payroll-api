using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.Common;
using MPS.Application.Features.Evaluation.OrderEvaluation.Commands;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;
using Xunit;
using ProjectEntity = MPS.Domain.Entities.Project;

namespace MPS.Application.Tests.Features.Evaluation;

public class OrderEvaluationWriteHandlerTests
{
    private readonly Mock<IOrderEvaluationRepository> _repo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IMonthlyAggregationService> _aggregation = new();

    private const int EvaluatorId = 5;
    private static readonly string RowVersion = Convert.ToBase64String(new byte[8]);

    private const int ProjectId = 7;

    private static MPS.Domain.Entities.Evaluation.OrderEvaluation Draft(int? nullScore = null, int responsibleUserId = EvaluatorId)
    {
        // 1 subject, 1 tiêu chí trọng số 100. nullScore=null → chấm 5; nullScore truyền vào để mô phỏng chưa chấm.
        // #63: quyền sửa theo Project.ResponsibleUserId (tổ trưởng ĐANG phụ trách), không phải EvaluatorUserId.
        return new MPS.Domain.Entities.Evaluation.OrderEvaluation
        {
            Id = 1, EvaluatorUserId = EvaluatorId, Status = EvaluationStatus.Draft,
            ProjectId = ProjectId,
            Project = new ProjectEntity { Id = ProjectId, ResponsibleUserId = responsibleUserId },
            PeriodYear = 2026, PeriodMonth = 7,
            Subjects = new List<OrderEvaluationSubject>
            {
                new()
                {
                    Id = 1, EmployeeUserId = 10, SubjectRoleIdSnapshot = SystemRoles.EmployeeId,
                    Scores = new List<EvaluationScoreLine>
                    {
                        new()
                        {
                            Id = 100, TypeNameSnapshot = "A", MetricNameSnapshot = "a", TypeWeightSnapshot = 100,
                            MetricWeightSnapshot = 100, Scale = 5, Score = nullScore,
                        },
                    },
                },
            },
        };
    }

    private ScoreOrderEvaluationCommandHandler ScoreSut() => new(_repo.Object, _currentUser.Object);

    private SubmitOrderEvaluationCommandHandler SubmitSut() => new(
        _repo.Object, _currentUser.Object, _aggregation.Object,
        NullLogger<SubmitOrderEvaluationCommandHandler>.Instance);

    private static ScoreOrderEvaluationCommand ScoreCmd(int score) =>
        new(1, RowVersion, new[]
        {
            new ScoreSubjectInput(1, null, new[] { new ScoreLineInput(100, score, null) }),
        });

    // ===== Score =====

    [Fact]
    public async Task Score_Throws409_OnRowVersionConflict()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(EvaluatorId);
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Draft());
        _repo.Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => ScoreSut().Handle(ScoreCmd(4), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Score_Throws403_WhenNotCurrentResponsibleLeader()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(999);
        _currentUser.Setup(c => c.HasPermission(Permissions.OrderEvaluationReadAll)).Returns(false);
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Draft());

        var act = () => ScoreSut().Handle(ScoreCmd(4), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // ===== #63/[D25] — quyền chấm chuyển theo tổ trưởng ĐANG phụ trách đơn =====

    [Fact]
    public async Task Score_Allows_NewResponsibleLeader_After_Handover()
    {
        // Đổi tổ trưởng: Evaluator cũ = 5, nay Project.ResponsibleUserId = 8. Tổ trưởng MỚI (8) phải sửa được.
        const int newLeader = 8;
        _currentUser.SetupGet(c => c.UserId).Returns(newLeader);
        _currentUser.Setup(c => c.HasPermission(Permissions.OrderEvaluationReadAll)).Returns(false);
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Draft(responsibleUserId: newLeader));
        _repo.Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ok = await ScoreSut().Handle(ScoreCmd(4), CancellationToken.None);

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task Score_Denies_Former_Evaluator_After_Handover()
    {
        // Tổ trưởng CŨ (== EvaluatorUserId 5) đã rời vị trí, Project.ResponsibleUserId nay = 8 ⇒ 5 bị chặn.
        _currentUser.SetupGet(c => c.UserId).Returns(EvaluatorId);
        _currentUser.Setup(c => c.HasPermission(Permissions.OrderEvaluationReadAll)).Returns(false);
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Draft(responsibleUserId: 8));

        var act = () => ScoreSut().Handle(ScoreCmd(4), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Score_Allows_ReadAll_Regardless_Of_Responsible()
    {
        // Quyền xem-tất-cả (vd SystemAdmin) vẫn sửa được dù không phụ trách.
        _currentUser.SetupGet(c => c.UserId).Returns(999);
        _currentUser.Setup(c => c.HasPermission(Permissions.OrderEvaluationReadAll)).Returns(true);
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Draft(responsibleUserId: 8));
        _repo.Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ok = await ScoreSut().Handle(ScoreCmd(4), CancellationToken.None);

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task Score_RecomputesSubjectFinalScore()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(EvaluatorId);
        MPS.Domain.Entities.Evaluation.OrderEvaluation? captured = null;
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Draft());
        _repo.Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
                It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.Evaluation.OrderEvaluation, byte[], CancellationToken>((e, _, _) => captured = e)
            .ReturnsAsync(true);

        await ScoreSut().Handle(ScoreCmd(4), CancellationToken.None);

        captured!.Subjects[0].FinalScore.Should().Be(4.00m);
        captured.Subjects[0].Scores[0].Score.Should().Be(4);
    }

    // ===== Submit =====

    [Fact]
    public async Task Submit_Blocked_WhenAnyScoreNull_E4()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(EvaluatorId);
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Draft(nullScore: null));

        var act = () => SubmitSut().Handle(new SubmitOrderEvaluationCommand(1, RowVersion), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _repo.Verify(r => r.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_SetsSubmitted_AndTriggersAggregation()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(EvaluatorId);
        var eval = Draft(nullScore: 5);   // đã chấm đủ
        MPS.Domain.Entities.Evaluation.OrderEvaluation? captured = null;
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(eval);
        _repo.Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.Evaluation.OrderEvaluation>(),
                It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.Evaluation.OrderEvaluation, byte[], CancellationToken>((e, _, _) => captured = e)
            .ReturnsAsync(true);

        var ok = await SubmitSut().Handle(new SubmitOrderEvaluationCommand(1, RowVersion), CancellationToken.None);

        ok.Should().BeTrue();
        captured!.Status.Should().Be(EvaluationStatus.Submitted);
        captured.SubmittedByUserId.Should().Be(EvaluatorId);
        _aggregation.Verify(a => a.AggregateAsync(2026, 7, null, EvaluatorId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
