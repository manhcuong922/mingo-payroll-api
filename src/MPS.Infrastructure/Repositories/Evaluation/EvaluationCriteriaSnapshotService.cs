using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Evaluation;

/// <summary>
/// Xây dòng snapshot tiêu chí (D2) từ catalog hiện có: <c>PerformanceMetric</c> có
/// <c>TargetRoles</c> chứa roleId. Xem plan §2, §13.6.
/// </summary>
public class EvaluationCriteriaSnapshotService : IEvaluationCriteriaSnapshotService
{
    private readonly AppDbContext _context;

    public EvaluationCriteriaSnapshotService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EvaluationScoreLine>> BuildLinesForRoleAsync(int roleId, CancellationToken ct)
    {
        var metrics = await _context.PerformanceMetrics
            .AsNoTracking()
            .Include(m => m.TypePerformanceMetric)
            .Where(m => m.TargetRoles.Any(r => r.Id == roleId))
            .ToListAsync(ct);

        if (metrics.Count == 0)
            throw new DomainValidationException(
                $"Chưa cấu hình tiêu chí đánh giá cho vai trò (role {roleId}). Vui lòng bổ sung tiêu chí trước khi chấm.");

        return metrics
            .Select(m => new EvaluationScoreLine
            {
                SourceMetricId = m.Id,
                TypeNameSnapshot = m.TypePerformanceMetric.Name,
                MetricNameSnapshot = m.Name,
                TypeWeightSnapshot = m.TypePerformanceMetric.Weight,
                MetricWeightSnapshot = m.Weight,
                Scale = 5,
                Score = null,
            })
            .ToList();
    }
}
