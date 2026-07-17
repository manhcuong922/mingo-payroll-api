using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Payroll;

/// <summary>1 field khả dụng cho trình dựng công thức: khoá, nhãn, module nguồn, kiểu dữ liệu.</summary>
public sealed record PayrollFieldDescriptor(string Key, string Label, string SourceModule, ColumnDataType DataType)
{
    public bool IsText => DataType == ColumnDataType.Text;
}

/// <summary>
/// Danh mục field (whitelist) cho trình dựng công thức — user chọn field từ đây. Đăng ký singleton.
/// </summary>
public interface IPayrollFieldCatalog
{
    IReadOnlyList<PayrollFieldDescriptor> GetAll();
    bool TryGet(string key, out PayrollFieldDescriptor descriptor);
    string LabelOf(string key);
}
