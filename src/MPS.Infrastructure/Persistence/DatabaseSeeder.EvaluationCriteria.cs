using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// Seed bộ tiêu chí đánh giá mặc định (<see cref="EvaluationCriteriaSeedData"/>) vào catalog
    /// <c>TypePerformanceMetric</c> + <c>PerformanceMetric</c> (+ M2M <c>TargetRoles</c>) để module
    /// chấm điểm tháng chạy được ngay. Idempotent: bỏ qua nếu catalog đã có tiêu chí (admin tự cấu hình).
    /// </summary>
    private async Task SeedDefaultEvaluationCriteriaAsync(CancellationToken ct)
    {
        if (await _context.TypePerformanceMetrics.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("Evaluation criteria (TypePerformanceMetric) already present; skipping.");
            return;
        }

        // Các role được tham chiếu trong seed — load tracked để attach vào M2M TargetRoles.
        var roleIds = EvaluationCriteriaSeedData.Groups
            .SelectMany(g => g.Metrics)
            .SelectMany(m => m.TargetRoleIds)
            .Distinct()
            .ToList();

        var rolesById = await _context.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var missing = roleIds.Where(id => !rolesById.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "Không seed tiêu chí đánh giá: thiếu role {RoleIds} trong DB.", string.Join(", ", missing));
            return;
        }

        var groups = EvaluationCriteriaSeedData.Groups
            .Select(g => new TypePerformanceMetric
            {
                Name = g.TypeName,
                Weight = g.TypeWeight,
                PerformanceMetrics = g.Metrics
                    .Select(m => new PerformanceMetric
                    {
                        Name = m.Name,
                        Weight = m.Weight,
                        Description = m.Description,
                        TargetRoles = m.TargetRoleIds.Select(id => rolesById[id]).ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        await _context.TypePerformanceMetrics.AddRangeAsync(groups, ct);
        await _context.SaveChangesAsync(ct);

        var metricCount = groups.Sum(g => g.PerformanceMetrics.Count);
        _logger.LogInformation(
            "Seeded {Groups} nhóm tiêu chí + {Metrics} tiêu chí đánh giá mặc định (module chấm điểm tháng).",
            groups.Count, metricCount);
    }
}
