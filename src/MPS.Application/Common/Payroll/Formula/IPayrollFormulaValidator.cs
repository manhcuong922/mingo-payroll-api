using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Common.Payroll.Formula;

/// <summary>
/// Kiểm tra tính hợp lệ của tập cột trong 1 bảng công thức trước khi lưu:
/// mã cột hợp lệ/không trùng, AST công thức đúng cú pháp, field/param/hàm tồn tại,
/// và không có phụ thuộc vòng (chu trình). Trả danh sách lỗi (rỗng = hợp lệ).
/// </summary>
public interface IPayrollFormulaValidator
{
    IReadOnlyList<string> Validate(
        IReadOnlyList<SalaryColumnDefinition> columns,
        IReadOnlyCollection<string> knownParamKeys);
}
