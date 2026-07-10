using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;
using MPS.Domain.Enums.Evaluation;

namespace MPS.Domain.Entities.Evaluation;

/// <summary>
/// Bảng đánh giá THEO ĐƠN (Loại A) — auto sinh khi 1 <see cref="Project"/> chuyển Completed (D9).
/// Người chấm = tổ trưởng phụ trách đơn (<see cref="Project.ResponsibleUserId"/> lúc sinh).
/// Kỳ = tháng của <see cref="Project.CompletedAt"/>. Draft → Submit (chốt, không sửa nữa).
/// Cuối tháng các bảng ĐÃ Submit của cùng 1 NV được gộp trung bình cộng thành 1
/// <see cref="MonthlyEvaluation"/> (Source = Aggregated). Xem plan §3.1, §4.
/// </summary>
[Auditable]
public class OrderEvaluation
{
    public int Id { get; set; }

    // 1 đơn ↔ 1 bảng (unique) — idempotent khi re-complete (E8).
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    // Tổ trưởng phụ trách đơn = người chấm (chốt lúc sinh).
    public int EvaluatorUserId { get; set; }
    public User? Evaluator { get; set; }

    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }

    public EvaluationStatus Status { get; set; } = EvaluationStatus.Draft;   // Draft | Submitted
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Optimistic concurrency — chống đua khi tổ trưởng chấm/submit đồng thời.
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<OrderEvaluationSubject> Subjects { get; set; } = new();
}
