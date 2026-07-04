using MPS.Domain.Attributes;

namespace MPS.Domain.Entities;

/// <summary>
/// Quy trình mẫu — CÔNG CỤ để sinh nhanh 1 loạt task khởi điểm cho đơn. KHÔNG liên kết sống:
/// sau khi GenerateTasks copy số liệu vào task, task tách rời, không tham chiếu ngược template.
/// </summary>
[Auditable]
public class ProcessTemplate
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;       // Unique, max 20 (vd: "KHUNG-A4")
    public string Name { get; set; } = string.Empty;       // max 150
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Xưởng mặc định áp dụng quy trình này (nullable — có thể chọn lúc phát đơn)
    public int? DefaultDepartmentId { get; set; }
    public Department? DefaultDepartment { get; set; }

    public List<ProcessStep> Steps { get; set; } = new();
}
