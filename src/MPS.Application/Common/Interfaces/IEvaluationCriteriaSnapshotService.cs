using MPS.Domain.Entities.Evaluation;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Xây các dòng snapshot tiêu chí (D2) từ RUBRIC CHUNG toàn công ty: mỗi <see cref="PerformanceMetric"/>
/// trong catalog → 1 <see cref="EvaluationScoreLine"/> mới (Score = null, Scale = 5), copy tên
/// nhóm/tiêu chí + trọng số. Rubric dùng CHUNG cho mọi vai trò (không còn lọc theo role — xem plan §2).
/// </summary>
public interface IEvaluationCriteriaSnapshotService
{
    /// <summary>
    /// Trả các dòng snapshot của rubric chung. Ném <c>DomainValidationException</c> nếu:
    /// (a) catalog rỗng (E3 — không sinh bảng rỗng vô nghĩa); hoặc
    /// (b) rubric chưa cân: tổng trọng số các nhóm ≠ 100, hoặc tổng trọng số tiêu chí trong bất kỳ
    ///     nhóm nào ≠ 100 (điểm tổng chỉ đúng khi trọng số tròn 100%).
    /// </summary>
    Task<IReadOnlyList<EvaluationScoreLine>> BuildLinesAsync(CancellationToken ct);
}
