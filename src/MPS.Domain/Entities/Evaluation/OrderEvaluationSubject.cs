namespace MPS.Domain.Entities.Evaluation;

/// <summary>
/// 1 công nhân bị chấm trong 1 bảng-theo-đơn. UNIQUE (OrderEvaluationId, EmployeeUserId).
/// <see cref="FinalScore"/> = điểm tổng theo trọng số 2 tầng (§5.1), null tới khi chấm đủ.
/// Xem plan §3.2.
/// </summary>
public class OrderEvaluationSubject
{
    public int Id { get; set; }

    public int OrderEvaluationId { get; set; }
    public OrderEvaluation? OrderEvaluation { get; set; }

    public int EmployeeUserId { get; set; }
    public User? Employee { get; set; }

    // Role tại thời điểm sinh bảng (thường 7 = Công nhân) — chốt để bộ tiêu chí không đổi khi NV đổi role (E6).
    public int SubjectRoleIdSnapshot { get; set; }

    // Thang 1–5 (decimal(4,2)).
    public decimal? FinalScore { get; set; }

    public string? Note { get; set; }

    public List<EvaluationScoreLine> Scores { get; set; } = new();
}
