namespace MPS.Domain.Enums.Evaluation;

/// <summary>
/// Nguồn của bảng tháng (<c>MonthlyEvaluation</c>).
/// <see cref="Aggregated"/> = công nhân, gộp trung bình cộng từ các bảng-theo-đơn (Loại A).
/// <see cref="Direct"/> = tổ trưởng/văn phòng, Director chấm trực tiếp theo tháng (Loại B).
/// Xem plan §1, §3.6.
/// </summary>
public enum EvaluationSource
{
    Aggregated = 0,
    Direct = 1,
}
