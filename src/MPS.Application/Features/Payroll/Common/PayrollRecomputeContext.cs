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
    private sealed record ParamSnap(string Key, string Name, decimal Value);

    public static PayrollCalcContext Build(PayrollPeriod period, PayrollRow row, IPayrollFieldCatalog catalog)
    {
        var inputs = PayrollEmployeeInputs.FromSnapshotJson(row.EmployeeInformationId ?? 0, row.InputSnapshotJson);

        var paramList = PayrollJson.Deserialize<List<ParamSnap>>(period.ParamsSnapshotJson) ?? new();
        var paramValues = paramList.ToDictionary(
            p => p.Key, p => new ResolvedValue(p.Value, p.Name), StringComparer.OrdinalIgnoreCase);

        var brackets = PayrollJson.Deserialize<List<PitBracketSnapshot>>(period.PitBracketsSnapshotJson) ?? new();

        return new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            Params = paramValues,
            FunctionContext = new PayrollFunctionContext { PitBrackets = brackets },
        };
    }
}
