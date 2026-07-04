using MPS.Domain.Attributes;
using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>Bước chuẩn trong quy trình mẫu (công đoạn). StepOrder chỉ để gợi ý thứ tự hiển thị.</summary>
[Auditable]
public class ProcessStep
{
    public int Id { get; set; }
    public int ProcessTemplateId { get; set; }
    public ProcessTemplate? ProcessTemplate { get; set; }

    public string Name { get; set; } = string.Empty;       // max 150
    public string? Description { get; set; }
    public int StepOrder { get; set; }                     // thứ tự công đoạn (1,2,3...) — chỉ để hiển thị
    public ProgressType DefaultProgressType { get; set; }  // mặc định cho task sinh ra (Quantity | Milestone)
    public int DefaultWeight { get; set; }                 // trọng số mặc định (dùng khi bước là Milestone)
    public int? Priority { get; set; }                     // 1=Low, 2=Medium, 3=High
}
