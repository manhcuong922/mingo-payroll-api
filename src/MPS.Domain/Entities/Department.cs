using MPS.Domain.Attributes;

namespace MPS.Domain.Entities;

[Auditable]
public class Department
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? ManagerId { get; set; }
    public EmployeeInformation? Manager { get; set; }

    // Soft-delete: "giải tán xưởng" = set false. KHÔNG hard-delete để giữ lịch sử đơn/task.
    public bool IsActive { get; set; } = true;

    public List<EmployeeInformation> Employees { get; set; } = new();
}
