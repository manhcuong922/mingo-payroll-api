using MPS.Domain.Entities.Evaluation;

namespace MPS.Domain.Interfaces.Evaluation;

/// <summary>1 đóng góp (bảng-theo-đơn) vào bảng tháng khi gộp — subjectId + điểm snapshot.</summary>
public readonly record struct AggregatedContribution(int OrderEvaluationSubjectId, decimal Score);

/// <summary>Kết quả upsert 1 bảng tháng Aggregated khi gộp.</summary>
public enum MonthlyAggregateOutcome
{
    Created,
    Updated,
    SkippedApproved,   // bảng đã Approved (khóa) → không đụng, cần HR Reopen (D8/E12)
}

public interface IMonthlyEvaluationRepository
{
    Task<MonthlyEvaluation?> GetByIdWithGraphAsync(int id, CancellationToken ct);   // AsNoTracking — display

    /// <summary>TRACKED, Include SourceLinks + Scores (KHÔNG nav User) — cho luồng ghi (score/approve/reopen).</summary>
    Task<MonthlyEvaluation?> GetByIdForUpdateAsync(int id, CancellationToken ct);

    Task<MonthlyEvaluation?> GetByEmployeeMonthAsync(int employeeUserId, int year, int month, CancellationToken ct);

    Task<(IReadOnlyList<MonthlyEvaluation> Items, int Total)> GetPagedByMonthAsync(
        int year, int month, int? departmentId, int page, int pageSize, CancellationToken ct);

    /// <summary>Bảng tháng của 1 NV (mọi kỳ, mới nhất trước) — cho màn "điểm của tôi" (Read.Self).</summary>
    Task<IReadOnlyList<MonthlyEvaluation>> GetForEmployeeAsync(int employeeUserId, CancellationToken ct);

    Task<int> AddAsync(MonthlyEvaluation entity, CancellationToken ct);

    Task<bool> UpdateWithConcurrencyAsync(MonthlyEvaluation entity, byte[] expectedRowVersion, CancellationToken ct);

    /// <summary>
    /// Upsert bảng tháng Aggregated cho (NV, Y, M): set <paramref name="finalScore"/> (đã tính sẵn),
    /// refresh SourceLinks, Status = Draft. Bỏ qua nếu bảng đã Approved (khóa). Atomic (1 SaveChanges).
    /// Xem plan §4.3.
    /// </summary>
    Task<MonthlyAggregateOutcome> UpsertAggregatedAsync(
        int employeeUserId, int year, int month, int subjectRoleId, int actorUserId,
        decimal finalScore, IReadOnlyList<AggregatedContribution> contributions, CancellationToken ct);
}
