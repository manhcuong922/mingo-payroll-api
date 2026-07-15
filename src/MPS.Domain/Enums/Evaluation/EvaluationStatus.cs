namespace MPS.Domain.Enums.Evaluation;

/// <summary>
/// Trạng thái bảng đánh giá. <see cref="Submitted"/> chỉ dùng cho bảng-theo-đơn
/// (<c>OrderEvaluation</c> — tổ trưởng chốt); <see cref="Approved"/> chỉ dùng cho
/// bảng tháng (<c>MonthlyEvaluation</c> — HR duyệt + khóa). Xem plan §3.6.
/// </summary>
public enum EvaluationStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
}
