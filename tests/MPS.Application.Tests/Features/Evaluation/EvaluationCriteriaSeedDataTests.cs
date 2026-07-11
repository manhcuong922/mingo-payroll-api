using FluentAssertions;
using MPS.Infrastructure.Persistence;
using Xunit;

namespace MPS.Application.Tests.Features.Evaluation;

public class EvaluationCriteriaSeedDataTests
{
    [Fact]
    public void TotalGroupWeight_Equals100()
    {
        // Rubric chung: tổng trọng số các NHÓM phải tròn 100% (điểm tổng mới đúng).
        var total = EvaluationCriteriaSeedData.Groups.Sum(g => g.TypeWeight);
        total.Should().Be(100m, "tổng trọng số các nhóm rubric phải = 100%");
    }

    [Fact]
    public void EveryGroup_MetricWeightSum_Equals100()
    {
        // Trong mỗi nhóm, tổng trọng số tiêu chí con phải tròn 100%.
        foreach (var group in EvaluationCriteriaSeedData.Groups)
        {
            var sum = group.Metrics.Sum(m => m.Weight);
            sum.Should().Be(100m, $"tổng trọng số tiêu chí trong nhóm '{group.TypeName}' phải = 100%");
        }
    }

    [Fact]
    public void EveryGroupAndMetric_HasPositiveWeight_AndNonEmptyText()
    {
        foreach (var group in EvaluationCriteriaSeedData.Groups)
        {
            group.TypeName.Should().NotBeNullOrWhiteSpace();
            group.TypeWeight.Should().BeGreaterThan(0m);
            group.Metrics.Should().NotBeEmpty();

            foreach (var metric in group.Metrics)
            {
                metric.Name.Should().NotBeNullOrWhiteSpace();
                metric.Description.Should().NotBeNullOrWhiteSpace();
                metric.Weight.Should().BeGreaterThan(0m);
            }
        }
    }

    [Fact]
    public void GroupNames_AreUnique()
    {
        var names = EvaluationCriteriaSeedData.Groups.Select(g => g.TypeName).ToList();
        names.Should().OnlyHaveUniqueItems();
    }
}
