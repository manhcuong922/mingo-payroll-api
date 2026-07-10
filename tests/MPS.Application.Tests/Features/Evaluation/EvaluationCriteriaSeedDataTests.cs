using FluentAssertions;
using MPS.Domain.Constants;
using MPS.Infrastructure.Persistence;
using Xunit;

namespace MPS.Application.Tests.Features.Evaluation;

public class EvaluationCriteriaSeedDataTests
{
    [Fact]
    public void EveryGroup_MetricWeightSum_IsWithinCatalogConstraint()
    {
        // Ràng buộc catalog: tổng Weight tiêu chí con trong 1 nhóm ≤ 100 (và > 0).
        foreach (var group in EvaluationCriteriaSeedData.Groups)
        {
            var sum = group.Metrics.Sum(m => m.Weight);
            sum.Should().BeGreaterThan(0m, $"nhóm '{group.TypeName}' phải có tiêu chí");
            sum.Should().BeLessThanOrEqualTo(100m, $"tổng trọng số nhóm '{group.TypeName}' vượt 100");
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
                metric.TargetRoleIds.Should().NotBeEmpty();
            }
        }
    }

    [Theory]
    [InlineData(SystemRoles.EmployeeId)]           // 7 — công nhân (auto sinh bảng-theo-đơn)
    [InlineData(SystemRoles.ProductionTeamLeaderId)]// 6 — tổ trưởng (Direct)
    [InlineData(SystemRoles.PlanningManagerId)]    // 5 — planning manager (Direct)
    [InlineData(SystemRoles.AccountantId)]         // 3 — kế toán (Direct)
    [InlineData(SystemRoles.HrId)]                 // 2 — HR (Direct)
    public void EveryScorableRole_HasAtLeastOneCriterion(int roleId)
    {
        var covered = EvaluationCriteriaSeedData.Groups
            .SelectMany(g => g.Metrics)
            .Any(m => m.TargetRoleIds.Contains(roleId));

        covered.Should().BeTrue($"role {roleId} phải có ít nhất 1 tiêu chí đánh giá");
    }

    [Fact]
    public void GroupNames_AreUnique()
    {
        var names = EvaluationCriteriaSeedData.Groups.Select(g => g.TypeName).ToList();
        names.Should().OnlyHaveUniqueItems();
    }
}
