using MPS.Domain.Entities.Evaluation;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Xây các dòng snapshot tiêu chí (D2) cho 1 role: mỗi <see cref="PerformanceMetric"/> có
/// <c>TargetRoles</c> chứa roleId → 1 <see cref="EvaluationScoreLine"/> mới (Score = null, Scale = 5),
/// copy tên nhóm/tiêu chí + trọng số. Xem plan §2, §13.6.
/// </summary>
public interface IEvaluationCriteriaSnapshotService
{
    /// <summary>
    /// Trả các dòng snapshot cho 1 role. Ném <c>DomainValidationException</c> nếu KHÔNG có tiêu chí
    /// nào cho role (E3 — không sinh bảng rỗng vô nghĩa).
    /// </summary>
    Task<IReadOnlyList<EvaluationScoreLine>> BuildLinesForRoleAsync(int roleId, CancellationToken ct);
}
