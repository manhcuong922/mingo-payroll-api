using FluentAssertions;
using MPS.Application.Tests.TestHelpers;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Evaluation;
using Xunit;

namespace MPS.Application.Tests.Features.Evaluation;

public class ApproveMonthlyEvaluationHandlerTests
{
    private readonly Mock<IMonthlyEvaluationRepository> _repo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IContractLookupReader> _lookup = new();

    private static readonly string RowVersion = Convert.ToBase64String(new byte[8]);

    private ApproveMonthlyEvaluationCommandHandler Sut() =>
        new(_repo.Object, _currentUser.Object, _lookup.Object, new NoopPayrollSourceChangeHandler());

    private void ReturnBoard(MPS.Domain.Entities.Evaluation.MonthlyEvaluation board)
    {
        _currentUser.SetupGet(c => c.UserId).Returns(2);
        _repo.Setup(r => r.GetByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(board);
    }

    [Fact]
    public async Task Blocks_Aggregated_WhenNoSourceLinks()
    {
        ReturnBoard(new MPS.Domain.Entities.Evaluation.MonthlyEvaluation
        {
            Id = 1, Source = EvaluationSource.Aggregated, Status = EvaluationStatus.Draft,
            SourceLinks = new List<MonthlyEvaluationSourceLink>(),
        });

        var act = () => Sut().Handle(new ApproveMonthlyEvaluationCommand(1, RowVersion), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _repo.Verify(r => r.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.Evaluation.MonthlyEvaluation>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Blocks_Direct_WhenScoreNull()
    {
        ReturnBoard(new MPS.Domain.Entities.Evaluation.MonthlyEvaluation
        {
            Id = 1, Source = EvaluationSource.Direct, Status = EvaluationStatus.Draft,
            Scores = new List<EvaluationScoreLine>
            {
                new() { Id = 1, TypeNameSnapshot = "A", MetricNameSnapshot = "a", MetricWeightSnapshot = 100, TypeWeightSnapshot = 100, Score = null },
            },
        });

        var act = () => Sut().Handle(new ApproveMonthlyEvaluationCommand(1, RowVersion), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task Approves_Direct_WhenAllScored()
    {
        MPS.Domain.Entities.Evaluation.MonthlyEvaluation? captured = null;
        ReturnBoard(new MPS.Domain.Entities.Evaluation.MonthlyEvaluation
        {
            Id = 1, Source = EvaluationSource.Direct, Status = EvaluationStatus.Draft,
            Scores = new List<EvaluationScoreLine>
            {
                new() { Id = 1, TypeNameSnapshot = "A", MetricNameSnapshot = "a", MetricWeightSnapshot = 100, TypeWeightSnapshot = 100, Score = 4 },
            },
        });
        _repo.Setup(r => r.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.Evaluation.MonthlyEvaluation>(),
                It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.Evaluation.MonthlyEvaluation, byte[], CancellationToken>((e, _, _) => captured = e)
            .ReturnsAsync(true);

        var ok = await Sut().Handle(new ApproveMonthlyEvaluationCommand(1, RowVersion), CancellationToken.None);

        ok.Should().BeTrue();
        captured!.Status.Should().Be(EvaluationStatus.Approved);
        captured.ApprovedByUserId.Should().Be(2);
    }
}
