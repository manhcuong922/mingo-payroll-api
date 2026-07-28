using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Common.Payroll;

/// <summary>
/// Tính giá trị các ô 1 dòng nhân viên theo bảng công thức, sinh explanation cho từng ô. Recompute
/// giữ ô override (cờ đỏ) và tính lại downstream. Dòng Total = tổng cột tiền.
/// </summary>
public interface IPayrollCalculator
{
    /// <summary>Tính đầy đủ 1 dòng (seed Input/Lookup + tính Formula theo topo). row.Cells được đổ mới.</summary>
    void ComputeAll(PayrollColumnPlan plan, PayrollCalcContext ctx, PayrollRow row);

    /// <summary>Tính lại downstream sau khi 1 cột đổi/override — giữ nguyên ô IsOverridden.</summary>
    void Recompute(PayrollColumnPlan plan, PayrollCalcContext ctx, PayrollRow row, string changedColumnCode);

    /// <summary>Dựng lại dòng Total = tổng các cột IsMoney trên các dòng nhân viên.</summary>
    void ComputeTotals(PayrollColumnPlan plan, IReadOnlyList<PayrollRow> employeeRows, PayrollRow totalRow);
}
