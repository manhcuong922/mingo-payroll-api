namespace MPS.Domain.Entities.Evaluation;

/// <summary>
/// Một dòng điểm cho 1 tiêu chí — dùng chung cho bảng-theo-đơn (thuộc
/// <see cref="OrderEvaluationSubject"/>) và bảng tháng Direct (thuộc <see cref="MonthlyEvaluation"/>).
/// ĐÚNG 1 trong 2 FK (<see cref="OrderEvaluationSubjectId"/> / <see cref="MonthlyEvaluationId"/>)
/// khác null.
///
/// Cốt lõi D2 — SNAPSHOT: tên nhóm/tiêu chí + trọng số được COPY vào đây lúc sinh bảng.
/// Sửa/xóa catalog (<see cref="PerformanceMetric"/>/<see cref="TypePerformanceMetric"/>) về sau
/// KHÔNG đụng bảng đã sinh. <see cref="SourceMetricId"/> chỉ để trace (SetNull khi metric bị xóa).
/// </summary>
public class EvaluationScoreLine
{
    public int Id { get; set; }

    // ĐÚNG 1 trong 2 khác null.
    public int? OrderEvaluationSubjectId { get; set; }
    public OrderEvaluationSubject? OrderEvaluationSubject { get; set; }
    public int? MonthlyEvaluationId { get; set; }
    public MonthlyEvaluation? MonthlyEvaluation { get; set; }

    // Ref tiêu chí gốc (nullable — tiêu chí có thể bị xóa sau, snapshot vẫn giữ nguyên).
    public int? SourceMetricId { get; set; }
    public PerformanceMetric? SourceMetric { get; set; }

    public string TypeNameSnapshot { get; set; } = string.Empty;
    public string MetricNameSnapshot { get; set; } = string.Empty;
    public decimal TypeWeightSnapshot { get; set; }
    public decimal MetricWeightSnapshot { get; set; }
    public int Scale { get; set; } = 5;

    // Điểm 1–5 (null tới khi người chấm cho điểm).
    public int? Score { get; set; }
    public string? Note { get; set; }
}
