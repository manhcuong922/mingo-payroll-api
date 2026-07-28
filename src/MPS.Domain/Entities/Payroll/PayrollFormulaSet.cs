using MPS.Domain.Attributes;

namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// "Bảng công thức" — tập định nghĩa cột + công thức mặc định. 1 công ty có nhiều bảng; mỗi kỳ
/// lương chọn 1 bảng và snapshot lại. Sửa bảng KHÔNG ảnh hưởng kỳ đã tạo (do snapshot).
/// </summary>
[Auditable]
public class PayrollFormulaSet
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Bảng mặc định gợi ý khi tạo kỳ mới. Chỉ 1 bảng IsDefault=true.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<SalaryColumnDefinition> Columns { get; set; } = new List<SalaryColumnDefinition>();
}
