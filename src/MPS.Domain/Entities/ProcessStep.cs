using MPS.Domain.Attributes;
using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Bước chuẩn trong quy trình mẫu. Step Quantity sinh CÔNG ĐOẠN (tầng 2, IsBatchParent=true) khi áp dụng
/// cho đơn. Step Milestone/Gate sinh GATE trực tiếp dưới Project (IsBatchParent=false, PlannedQuantity=0,
/// PlannedWorkUnits=0, IsRequiredGate=DefaultIsRequiredGate) — không tính % tiến độ, chỉ là điều kiện chặn.
/// StepOrder chỉ để gợi ý thứ tự hiển thị.
/// </summary>
[Auditable]
public class ProcessStep
{
    public int Id { get; set; }
    public int ProcessTemplateId { get; set; }
    public ProcessTemplate? ProcessTemplate { get; set; }

    public string Name { get; set; } = string.Empty;       // max 150
    public string? Description { get; set; }
    public int StepOrder { get; set; }                     // thứ tự công đoạn/gate (1,2,3...) — chỉ để hiển thị
    public ProgressType DefaultProgressType { get; set; }  // Quantity → sinh công đoạn; Milestone → sinh gate
    public int DefaultPlannedQuantity { get; set; }         // sản lượng kế hoạch mặc định (chỉ dùng khi Quantity)
    public int DefaultPlannedWorkUnits { get; set; }        // khối lượng công việc mặc định (chỉ dùng khi Quantity)
    public bool DefaultIsRequiredGate { get; set; } = true;  // chỉ dùng khi DefaultProgressType=Milestone
    // Legacy — không còn dùng cho tính % (thay bằng DefaultPlannedWorkUnits). Giữ cột DB tương thích dữ liệu cũ.
    public int DefaultWeight { get; set; }
    public int? Priority { get; set; }                     // 1=Low, 2=Medium, 3=High
}
