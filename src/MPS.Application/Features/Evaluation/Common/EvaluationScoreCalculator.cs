using MPS.Domain.Entities.Evaluation;

namespace MPS.Application.Features.Evaluation.Common;

/// <summary>
/// Công thức tính điểm 1 đối tượng trong 1 bảng (order-subject hoặc direct) — trọng số 2 tầng
/// (type × metric), thang 1–5. Xem plan §5.1.
///
/// <code>
/// TypeScore(t) = Σ_metric∈t (MetricWeightSnapshot × Score) / Σ_metric∈t MetricWeightSnapshot
/// FinalScore   = Σ_type (TypeWeightSnapshot × TypeScore(t)) / Σ_type TypeWeightSnapshot
/// </code>
/// - Bỏ qua dòng <c>Score = null</c> khỏi cả tử &amp; mẫu.
/// - Nhóm có Σ MetricWeightSnapshot = 0 (rỗng/trọng số 0) → loại khỏi mẫu, chống chia 0 (E11).
/// - Không dòng nào có điểm / tổng trọng số type = 0 → trả <c>null</c> (chưa tính được).
/// </summary>
public static class EvaluationScoreCalculator
{
    public static decimal? ComputeFinalScore(IEnumerable<EvaluationScoreLine> lines)
    {
        var scored = lines.Where(l => l.Score.HasValue).ToList();
        if (scored.Count == 0) return null;

        decimal weightedTypeSum = 0m;
        decimal typeWeightTotal = 0m;

        // Nhóm theo Type (tên + trọng số snapshot — cùng type có cùng cả hai).
        foreach (var group in scored.GroupBy(l => new { l.TypeNameSnapshot, l.TypeWeightSnapshot }))
        {
            var metricWeightSum = group.Sum(l => l.MetricWeightSnapshot);
            if (metricWeightSum <= 0m) continue;   // chống chia 0 (E11)

            var typeScore = group.Sum(l => l.MetricWeightSnapshot * l.Score!.Value) / metricWeightSum;
            var typeWeight = group.Key.TypeWeightSnapshot;

            weightedTypeSum += typeWeight * typeScore;
            typeWeightTotal += typeWeight;
        }

        if (typeWeightTotal <= 0m) return null;

        return Math.Round(weightedTypeSum / typeWeightTotal, 2, MidpointRounding.AwayFromZero);
    }
}
