using MPS.Application.Common.Payroll.Excel;

namespace MPS.Application.Common.Interfaces.Payroll;

/// <summary>
/// Render <see cref="PayrollExcelBook"/> (đã tính xong) thành file .xlsx. Toàn bộ quyết định bố cục
/// và công thức nằm ở builder — impl chỉ ghi OpenXml.
/// </summary>
public interface IPayrollExcelExporter
{
    byte[] Build(PayrollExcelBook book);
}
