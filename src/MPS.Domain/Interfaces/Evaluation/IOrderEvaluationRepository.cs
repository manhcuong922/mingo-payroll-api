using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Evaluation;

namespace MPS.Domain.Interfaces.Evaluation;

public interface IOrderEvaluationRepository
{
    /// <summary>Idempotent guard (E8) — đã có bảng cho đơn này chưa.</summary>
    Task<bool> ExistsForProjectAsync(int projectId, CancellationToken ct);

    /// <summary>
    /// Distinct UserId của mọi <c>TaskAssignee</c> dưới đơn (mọi tầng), lọc theo role đối tượng
    /// (thường 7 = Công nhân). Bỏ node container/gate không có assignee. Xem plan §4.1 bước 3.
    /// </summary>
    Task<IReadOnlyList<int>> GetSubjectUserIdsForProjectAsync(int projectId, int subjectRoleId, CancellationToken ct);

    Task<OrderEvaluation?> GetByIdWithGraphAsync(int id, CancellationToken ct);   // AsNoTracking — display (Project/Evaluator/Subjects.Employee.Scores)

    /// <summary>
    /// TRACKED, chỉ Include Subjects.Scores (KHÔNG nav User/Project) — cho luồng ghi (chấm/submit)
    /// để <c>Update</c> không đụng các bảng khác.
    /// </summary>
    Task<OrderEvaluation?> GetByIdForUpdateAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<OrderEvaluation>> GetForEvaluatorAsync(
        int evaluatorUserId, EvaluationStatus? status, int? departmentId, CancellationToken ct);

    /// <summary>Các subject của bảng-theo-đơn đã Submit trong kỳ (Y, M), điểm ≠ null — để gộp tháng.</summary>
    Task<IReadOnlyList<OrderEvaluationSubject>> GetSubmittedSubjectsByMonthAsync(
        int year, int month, int? onlyEmployeeUserId, CancellationToken ct);

    Task<int> AddAsync(OrderEvaluation entity, CancellationToken ct);

    Task<bool> UpdateWithConcurrencyAsync(OrderEvaluation entity, byte[] expectedRowVersion, CancellationToken ct);
}
