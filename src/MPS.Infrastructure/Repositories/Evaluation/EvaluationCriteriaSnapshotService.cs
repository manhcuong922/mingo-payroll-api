using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Evaluation;

/// <summary>
/// Xây dòng snapshot tiêu chí (D2) từ RUBRIC CHUNG toàn công ty: toàn bộ <c>PerformanceMetric</c>
/// trong catalog (không lọc theo role — rubric dùng chung mọi vai trò). Xem plan §2, §13.6.
/// </summary>
public class EvaluationCriteriaSnapshotService : IEvaluationCriteriaSnapshotService
{
    // Sai số cho phép khi so tổng trọng số (decimal, tránh lệch do nhập .5).
    private const decimal FullWeight = 100m;

    private readonly AppDbContext _context;

    public EvaluationCriteriaSnapshotService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EvaluationScoreLine>> BuildLinesAsync(CancellationToken ct)
    {
        var metrics = await _context.PerformanceMetrics
            .AsNoTracking()
            .Include(m => m.TypePerformanceMetric)
            .ToListAsync(ct);

        if (metrics.Count == 0)
            throw new DomainValidationException(
                "Chưa cấu hình tiêu chí đánh giá. Vui lòng thêm rubric ở trang Tiêu chí trước khi chấm.");

        // Rubric chỉ đúng khi trọng số tròn 100%: tổng các NHÓM = 100 và mỗi nhóm tổng TIÊU CHÍ = 100.
        var groups = metrics
            .GroupBy(m => new { m.TypePerformanceMetricId, m.TypePerformanceMetric.Name, m.TypePerformanceMetric.Weight })
            .ToList();

        var typeWeightTotal = groups.Sum(g => g.Key.Weight);
        if (typeWeightTotal != FullWeight)
            throw new DomainValidationException(
                $"Rubric chưa cân: tổng trọng số các nhóm đang là {typeWeightTotal}% (phải = 100%). " +
                "Vui lòng sửa ở trang Tiêu chí trước khi chấm.");

        foreach (var g in groups)
        {
            var metricWeightSum = g.Sum(m => m.Weight);
            if (metricWeightSum != FullWeight)
                throw new DomainValidationException(
                    $"Rubric chưa cân: tổng trọng số tiêu chí trong nhóm \"{g.Key.Name}\" đang là {metricWeightSum}% " +
                    "(phải = 100%). Vui lòng sửa ở trang Tiêu chí trước khi chấm.");
        }

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
