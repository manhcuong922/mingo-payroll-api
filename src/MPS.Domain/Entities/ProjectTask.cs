using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;
using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Task sản xuất (v8). Cây drill-down 3 tầng: Đơn → công đoạn (task cha IsBatchParent) → task thực thi (con).
/// Task con giao 1 người HOẶC nhiều người (multi-assignee qua <see cref="Assignees"/>), chung 1 Done.
/// ProgressType.Quantity = task/công đoạn sản lượng, tham gia tính % tiến độ (EV/PV theo số lượng).
/// ProgressType.Milestone = Gate/Checklist (điều kiện bắt buộc/tuỳ chọn), KHÔNG tham gia tính % —
/// chỉ có Status Done/Todo, dùng để chặn hoàn thành công đoạn/đóng đơn khi IsRequiredGate=true.
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
    // Không còn dùng ở tầng nghiệp vụ (tính năng "task bàn giao" đã bỏ) — giữ cột DB để tương thích dữ liệu cũ.
    public bool RequiresHandover { get; set; } = false;
    public bool IsConfidential { get; set; } = false;
    public DateTime? Deadline { get; set; }
    public int? Priority { get; set; }                         // 1=Low, 2=Medium, 3=High
    public int StepOrder { get; set; }                         // thứ tự công đoạn (copy từ ProcessStep khi generate)

    // --- Đo tiến độ (KHÔNG dùng giờ công) ---
    public int PlannedQuantity { get; set; }                   // Quantity: SP mục tiêu (cha = của chính cha, không phải SUM con). Gate: 0
    public int CompletedQuantity { get; set; }                 // Quantity: SP thực đạt (cha = SUM con Quantity). Gate: luôn 0
    // Legacy — không còn dùng cho tính % (Gate không có "trọng số quy đổi sản lượng"). Giữ cột DB tương thích dữ liệu cũ.
    public int Weight { get; set; }
    public DateTime? ProgressUpdatedAt { get; set; }           // lần cuối cập nhật CompletedQuantity

    // Trọng số/capacity ở MỌI node của cây N tầng (refactor PR). Container: mẫu số rollup con (capacity riêng
    // của node — Σ con < capacity ⇒ % < 100%). Leaf: trọng số đóng góp lên node cha. KHÔNG scale theo quy mô đơn.
    public int PlannedWorkUnits { get; set; }

    // --- N-tier authoring (refactor) ---
    // Số người cố định của vị trí lá (copy từ template, KHÔNG scale). Gán < / > → cảnh báo mềm ở tầng handler.
    public int PlannedHeadcount { get; set; }
    // Chức danh bắt buộc để gán người vào vị trí lá (FK Position). Gán sai chức danh → 400.
    public int? RequiredPositionId { get; set; }
    public Position? RequiredPosition { get; set; }
    // Vị trí tương đối trong timeline đơn (0..1): task.Deadline = StartDate + ratio × (Deadline − StartDate).
    // null ⇒ task không có deadline dẫn xuất. Sửa deadline tay → cập nhật lại ratio (giữ vị trí tương đối).
    public double? DeadlineOffsetRatio { get; set; }

    // Gate/Checklist bắt buộc (chỉ có ý nghĩa khi ProgressType=Milestone). Gate có thể là task lá dưới 1
    // công đoạn (ParentTaskId != null, chặn công đoạn cha tự Done) hoặc sinh trực tiếp dưới Project từ
    // ProcessStep Milestone (ParentTaskId = null, IsBatchParent = false, chỉ chặn đóng đơn) —
    // true = phải Done mới cho công đoạn cha tự Done / đóng đơn chặn nếu còn gate bắt buộc chưa Done.
    public bool IsRequiredGate { get; set; } = true;

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

    // true = node CONTAINER (gộp con) ở BẤT KỲ tầng nào — không giao ai/không sản lượng trực tiếp;
    // tiến độ = rollup Children. false = work-leaf (Quantity) hoặc gate (Milestone). Refactor: bỏ ràng buộc
    // "cây chỉ 3 tầng" — container lồng container tùy ý (cap MaxTreeDepth). Suy được từ "có con" nhưng giữ cột
    // để query/rollup không phải join Children mỗi lần.
    public bool IsBatchParent { get; set; } = false;

    // ⭐ Multi-assignee: 1 task giao 1 HOẶC nhiều người (bảng nối). Node container để rỗng.
    public List<TaskAssignee> Assignees { get; set; } = new();

    // Tài liệu/ảnh: KHÔNG navigation — ProductionAttachment polymorphic (OwnerType+OwnerId), query qua repo.

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    // Optimistic concurrency — chống đua khi 2 lần cập nhật sản lượng / complete đồng thời.
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // --- Pause/Cancel lan truyền theo tầng (plan mục 6.5) ---
    // null = Pause trực tiếp (được Resume độc lập); khác null = Pause do lan truyền từ cha
    // (trỏ tới Id của bản ghi cha đã ra lệnh) → chỉ Resume được khi cha Resume trước.
    public int? PausedByCascadeFromId { get; set; }

    // Status trước khi Pause/Cancel — Resume/Restore trả lại đúng giá trị này (Todo/InProgress/Done).
    public TaskItemStatus? StatusBeforePause { get; set; }

    // Soft-delete cho Cancel — bản ghi vẫn tồn tại trong DB, ẩn khỏi danh sách mặc định.
    public DateTime? DeletedAt { get; set; }
}
