using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// Seed bộ tiêu chí đánh giá mặc định (<see cref="EvaluationCriteriaSeedData"/>) — RUBRIC CHUNG
    /// toàn công ty — vào catalog <c>TypePerformanceMetric</c> + <c>PerformanceMetric</c> để module
    /// chấm điểm chạy được ngay. Idempotent: bỏ qua nếu catalog đã có tiêu chí (admin tự cấu hình).
    /// Rubric dùng chung mọi vai trò nên KHÔNG gán <c>TargetRoles</c> (không còn lọc theo role).
    /// </summary>
    private async Task SeedDefaultEvaluationCriteriaAsync(CancellationToken ct)
    {
        if (await _context.TypePerformanceMetrics.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("Evaluation criteria (TypePerformanceMetric) already present; skipping.");
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
                    })
                    .ToList(),
            })
            .ToList();

        await _context.TypePerformanceMetrics.AddRangeAsync(groups, ct);
        await _context.SaveChangesAsync(ct);

        var metricCount = groups.Sum(g => g.PerformanceMetrics.Count);
        _logger.LogInformation(
            "Seeded {Groups} nhóm tiêu chí + {Metrics} tiêu chí đánh giá mặc định (rubric chung toàn công ty).",
            groups.Count, metricCount);
    }
}
