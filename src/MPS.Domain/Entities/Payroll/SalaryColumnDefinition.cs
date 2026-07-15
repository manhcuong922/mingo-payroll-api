using MPS.Domain.Attributes;
using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// Định nghĩa 1 cột lương. Cột mặc định lẫn cột user thêm đều là bản ghi này (mô hình thống nhất).
/// Giá trị mỗi (dòng, cột) lưu ở <see cref="PayrollCellValue"/> theo <see cref="ColumnCode"/>.
/// </summary>
[Auditable]
public class SalaryColumnDefinition
{
    public int Id { get; set; }

    public int FormulaSetId { get; set; }
    public PayrollFormulaSet FormulaSet { get; set; } = null!;

    /// <summary>Mã cột duy nhất trong 1 bảng công thức (vd MUCLUONG, THUNHAP). Công thức tham chiếu qua mã này.</summary>
    public string ColumnCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>Header nhóm cha (merge 3 tầng đặc tả): vd "Bảo hiểm", "Thu nhập chịu thuế".</summary>
    public string? GroupName { get; set; }
    public int DisplayOrder { get; set; }

    public ColumnDataType DataType { get; set; }
    public ColumnValueSource ValueSource { get; set; }

    /// <summary>ValueSource=Lookup: khoá field trong IPayrollFieldCatalog (vd "attendance.congTinhCom").</summary>
    public string? LookupKey { get; set; }

    /// <summary>ValueSource=Formula: AST biểu thức serialize JSON.</summary>
    public string? FormulaJson { get; set; }

    public RoundingMode Rounding { get; set; } = RoundingMode.None;

    /// <summary>Cột hệ thống (mặc định) — sửa công thức được nhưng KHÔNG xoá.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Đưa vào dòng TỔNG CỘNG (SUMIFS toàn cột tiền).</summary>
    public bool IsMoney { get; set; }

    /// <summary>Cho phép user ghi đè giá trị ô (override).</summary>
    public bool IsEditable { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
