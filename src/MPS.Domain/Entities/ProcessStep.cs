using MPS.Domain.Attributes;
using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Node trong quy trình mẫu — cây N tầng tự tham chiếu (refactor task-full-generation).
/// Mỗi node là 1 trong 3 loại (suy từ cấu trúc + <see cref="DefaultProgressType"/>):
///   - Container (có ≥1 con): node gộp, không sản lượng/không headcount trực tiếp; % = rollup con.
///   - Work-leaf (không con, ProgressType=Quantity): vị trí gán được — có DefaultPlannedQuantity,
///     PlannedHeadcount, RequiredPositionId, ScalesWithQuantity.
///   - Gate (ProgressType=Milestone): điều kiện chặn (DefaultIsRequiredGate), không sản lượng, không %.
/// Khi áp cho đơn → sinh full cây task N tầng, scale sản lượng theo Project.Quantity / Template.BaseQuantity.
/// </summary>
[Auditable]
public class ProcessStep
{
    public int Id { get; set; }
    public int ProcessTemplateId { get; set; }
    public ProcessTemplate? ProcessTemplate { get; set; }

    // Cây N tầng trong template (self-ref). null = node top-level dưới template.
    public int? ParentProcessStepId { get; set; }
    public ProcessStep? Parent { get; set; }
    public List<ProcessStep> Children { get; set; } = new();

    public string Name { get; set; } = string.Empty;       // max 150
    public string? Description { get; set; }
    public int StepOrder { get; set; }                     // thứ tự hiển thị trong cùng 1 cấp (1,2,3...)
    public ProgressType DefaultProgressType { get; set; }  // Quantity → work-leaf/container; Milestone → gate
    public int DefaultPlannedQuantity { get; set; }        // sản lượng cơ sở của lá (chỉ dùng khi Quantity + là lá)
    public int DefaultPlannedWorkUnits { get; set; }       // trọng số/capacity ở MỌI node (KHÔNG scale)
    public bool DefaultIsRequiredGate { get; set; } = true; // chỉ dùng khi DefaultProgressType=Milestone

    // --- N-tier authoring (refactor) ---
    public int PlannedHeadcount { get; set; }              // số người cố định của vị trí lá (KHÔNG scale)
    public int? RequiredPositionId { get; set; }           // chức danh bắt buộc để gán vào vị trí lá (FK Position)
    public Position? RequiredPosition { get; set; }
    public bool ScalesWithQuantity { get; set; } = true;   // lá: true = sản lượng scale theo quy mô đơn; false = giữ nguyên
    public double? DefaultDeadlineOffsetRatio { get; set; } // vị trí tương đối trong timeline đơn (0..1); null = không deadline

    // Legacy — không còn dùng cho tính % (thay bằng DefaultPlannedWorkUnits). Giữ cột DB tương thích dữ liệu cũ.
    public int DefaultWeight { get; set; }
    public int? Priority { get; set; }                     // 1=Low, 2=Medium, 3=High
}
