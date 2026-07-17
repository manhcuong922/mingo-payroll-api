using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// Dựng lại PayrollCalcContext cho 1 dòng từ SNAPSHOT của kỳ (input NV + params + biểu thuế) — recompute
/// sau override không phụ thuộc dữ liệu module hiện tại (bất biến).
/// </summary>
public static class PayrollRecomputeContext
{
    public static PayrollCalcContext Build(PayrollPeriod period, PayrollRow row, IPayrollFieldCatalog catalog)
    {
        var inputs = PayrollEmployeeInputs.FromSnapshotJson(row.EmployeeInformationId ?? 0, row.InputSnapshotJson);

        var brackets = PayrollJson.Deserialize<List<PitBracketSnapshot>>(period.PitBracketsSnapshotJson) ?? new();

        return new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            FunctionContext = new PayrollFunctionContext { PitBrackets = brackets },
        };
    }
}
