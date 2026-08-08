namespace MPS.Domain.Constants;

/// <summary>
/// Phạm vi đối tượng bị chấm của module đánh giá tháng (plan §1, D6).
///
/// Loại A — Công nhân (<see cref="SystemRoles.EmployeeId"/>): chấm qua bảng-theo-đơn rồi gộp tháng.
/// Loại B — <see cref="DirectScorableRoleIds"/>: Director chấm trực tiếp 1 bảng/tháng; job
/// <c>DirectEvaluationAutoGenerateJob</c> sinh sẵn bảng điểm mặc định đầu tháng sau.
/// </summary>
public static class EvaluationSubjectRoles
{
    /// <summary>
    /// Các role được chấm TRỰC TIẾP theo tháng (Loại B). Nguồn sự thật duy nhất — job auto-sinh,
    /// command tạo tay và UI đều đọc từ đây; đổi phạm vi chỉ sửa 1 chỗ.
    /// </summary>
    public static readonly IReadOnlyList<int> DirectScorableRoleIds = new[]
    {
        SystemRoles.ProductionTeamLeaderId,   // 6 — Tổ trưởng sản xuất
        SystemRoles.PlanningManagerId,        // 5 — Planning Manager
        SystemRoles.AccountantId,             // 3 — Kế toán
        SystemRoles.HrId,                     // 2 — HR
    };

    /// <summary>
    /// Điểm mặc định mọi tiêu chí khi hệ thống sinh sẵn bảng Direct — 4/5 "Đạt yêu cầu".
    /// Director không sửa ⇒ giữ nguyên điểm này (FinalScore = 4.00 do rubric cân 100%/100%).
    /// </summary>
    public const int DefaultDirectScore = 4;
}
