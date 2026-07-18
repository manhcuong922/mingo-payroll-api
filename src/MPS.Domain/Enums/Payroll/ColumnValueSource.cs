namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Nguồn giá trị của 1 cột lương:
/// - Input: người dùng nhập tay (vd MUCLUONG, cờ BH/thuế, phụ cấp nhập tay).
/// - Lookup: lấy trực tiếp từ module khác qua <c>LookupKey</c> (field trong IPayrollFieldCatalog).
/// - Formula: tính bằng biểu thức <c>FormulaJson</c> (tham chiếu cột khác + field + hàm dựng sẵn).
/// </summary>
public enum ColumnValueSource
{
    Input = 1,
    Lookup = 2,
    Formula = 3,
}
