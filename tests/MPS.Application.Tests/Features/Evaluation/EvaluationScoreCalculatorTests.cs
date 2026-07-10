using FluentAssertions;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Entities.Evaluation;
using Xunit;

namespace MPS.Application.Tests.Features.Evaluation;

public class EvaluationScoreCalculatorTests
{
    private static EvaluationScoreLine Line(string type, decimal typeWeight, decimal metricWeight, int? score) => new()
    {
        TypeNameSnapshot = type,
        TypeWeightSnapshot = typeWeight,
        MetricNameSnapshot = $"{type}-metric",
        MetricWeightSnapshot = metricWeight,
        Scale = 5,
        Score = score,
    };

    [Fact]
    public void WeightedWithinSingleType()
    {
        // (60*5 + 40*3) / 100 = 4.2 ; 1 nhóm trọng số 100 → 4.2
        var lines = new[] { Line("A", 100, 60, 5), Line("A", 100, 40, 3) };
        EvaluationScoreCalculator.ComputeFinalScore(lines).Should().Be(4.20m);
    }

    [Fact]
    public void WeightedAcrossTwoTypes()
    {
        // TypeScore A = 5, B = 1 ; final = (70*5 + 30*1) / 100 = 3.8
        var lines = new[] { Line("A", 70, 100, 5), Line("B", 30, 100, 1) };
        EvaluationScoreCalculator.ComputeFinalScore(lines).Should().Be(3.80m);
    }

    [Fact]
    public void SkipsZeroMetricWeightGroup_NoDivideByZero()
    {
        // Nhóm A có Σ metricWeight = 0 → loại khỏi mẫu (E11); final = (50*4) / 50 = 4.0
        var lines = new[] { Line("A", 50, 0, 5), Line("B", 50, 100, 4) };
        EvaluationScoreCalculator.ComputeFinalScore(lines).Should().Be(4.00m);
    }

    [Fact]
    public void AllNullScores_ReturnsNull()
    {
        var lines = new[] { Line("A", 100, 50, null), Line("A", 100, 50, null) };
        EvaluationScoreCalculator.ComputeFinalScore(lines).Should().BeNull();
    }

    [Fact]
    public void IgnoresNullScoredLines()
    {
        // Chỉ dòng đã chấm tính vào: (50*4) / 50 = 4.0
        var lines = new[] { Line("A", 100, 50, 4), Line("A", 100, 50, null) };
        EvaluationScoreCalculator.ComputeFinalScore(lines).Should().Be(4.00m);
    }

    [Fact]
    public void RoundsToTwoDecimals_AwayFromZero()
    {
        // (5+4+4)/3 = 4.333... → 4.33
        var lines = new[] { Line("A", 100, 1, 5), Line("A", 100, 1, 4), Line("A", 100, 1, 4) };
        EvaluationScoreCalculator.ComputeFinalScore(lines).Should().Be(4.33m);
    }

    [Fact]
    public void EmptyLines_ReturnsNull()
    {
        EvaluationScoreCalculator.ComputeFinalScore(Array.Empty<EvaluationScoreLine>()).Should().BeNull();
    }
}
