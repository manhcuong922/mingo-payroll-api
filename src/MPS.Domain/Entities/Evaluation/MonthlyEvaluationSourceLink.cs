namespace MPS.Domain.Entities.Evaluation;

/// <summary>
/// Trace gộp tháng (chỉ Loại A / Source = Aggregated): mỗi bảng-theo-đơn đã Submit của NV
/// góp 1 dòng vào bảng tháng. <see cref="MonthlyEvaluation.FinalScore"/> = AVG(ContributedScore)
/// — trung bình cộng đơn giản (D7). Xem plan §3.4.
/// </summary>
public class MonthlyEvaluationSourceLink
{
    public int Id { get; set; }

    public int MonthlyEvaluationId { get; set; }
    public MonthlyEvaluation? MonthlyEvaluation { get; set; }

    public int OrderEvaluationSubjectId { get; set; }
    public OrderEvaluationSubject? OrderEvaluationSubject { get; set; }

    // Snapshot điểm của bảng-theo-đơn lúc gộp (thang 1–5).
    public decimal ContributedScore { get; set; }
}
