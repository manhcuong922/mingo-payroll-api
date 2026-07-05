using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;
using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Task sản xuất (v5). Cây drill-down 3 tầng: Đơn → công đoạn (task cha IsBatchParent) → task thực thi (con).
/// Task con giao 1 người HOẶC nhiều người (multi-assignee qua <see cref="Assignees"/>), chung 1 Done.
/// Đo tiến độ LAI: ProgressType.Quantity → EV/PV theo SỐ LƯỢNG (không giờ công); Milestone → Weight nhập tay + đúng/chậm deadline.
/// Hoàn thành = quản lý xưởng bấm Complete (không duyệt); quá deadline vẫn Done nhưng gắn WasLate.
/// </summary>
[Auditable]
public class ProjectTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;          // max 200
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskItemType Type { get; set; } = TaskItemType.ProcessTask;
    public ProgressType ProgressType { get; set; } = ProgressType.Quantity;
    public bool RequiresHandover { get; set; } = false;        // true = task bàn giao: bắt buộc attachment khi Complete
    public bool IsConfidential { get; set; } = false;
    public DateTime? Deadline { get; set; }
    public int? Priority { get; set; }                         // 1=Low, 2=Medium, 3=High
    public int StepOrder { get; set; }                         // thứ tự công đoạn (copy từ ProcessStep khi generate)

    // --- Đo tiến độ (KHÔNG dùng giờ công) ---
    public int PlannedQuantity { get; set; }                   // Quantity: SP mục tiêu (cha = tổng con). Milestone: 0
    public int CompletedQuantity { get; set; }                 // Quantity: SP thực đạt (cha = SUM con). Milestone: 0
    public int Weight { get; set; }                            // Milestone: trọng số nhập tay (đóng góp vào tiến độ đơn)
    public DateTime? ProgressUpdatedAt { get; set; }           // lần cuối cập nhật CompletedQuantity

    // --- Hoàn thành (bấm tay, không duyệt) ---
    public int? CompletedByUserId { get; set; }
    public User? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool WasLate { get; set; }                          // Done sau Deadline → true (chậm tiến độ)

    // Quan hệ đơn (null = Internal Task)
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    // Xưởng phụ trách task (điểm neo scope v5)
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Self-referencing: cây drill-down 3 tầng. Task cha = 1 CÔNG ĐOẠN (tầng 2) bọc các task con (tầng 3).
    public int? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }
    public List<ProjectTask> Children { get; set; } = new();

    // true = task CÔNG ĐOẠN (tầng 2), không giao ai trực tiếp; tiến độ = tổng Children;
    // loại khỏi "task của tôi" của nhân viên.
    public bool IsBatchParent { get; set; } = false;

    // ⭐ Multi-assignee: 1 task giao 1 HOẶC nhiều người (bảng nối). Task cha (IsBatchParent) để rỗng.
    public List<TaskAssignee> Assignees { get; set; } = new();

    // Tài liệu/ảnh: KHÔNG navigation — ProductionAttachment polymorphic (OwnerType+OwnerId), query qua repo.

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    // Optimistic concurrency — chống đua khi 2 lần cập nhật sản lượng / complete đồng thời.
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
