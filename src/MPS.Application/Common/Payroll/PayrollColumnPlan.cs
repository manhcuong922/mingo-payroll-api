using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Payroll;

/// <summary>
/// Bản kế hoạch tính của 1 bảng công thức: parse FormulaJson 1 lần, sắp topo, phát hiện chu trình.
/// Tái dùng cho mọi dòng nhân viên (parse/sort không lặp lại per-row).
/// </summary>
public sealed class PayrollColumnPlan
{
    public required IReadOnlyList<SalaryColumnDefinition> OrderedColumns { get; init; }
    public required IReadOnlyDictionary<string, SalaryColumnDefinition> ByCode { get; init; }
    public required IReadOnlyDictionary<string, FormulaNode?> Formulas { get; init; }

    public static PayrollColumnPlan Build(IReadOnlyList<SalaryColumnDefinition> columns, IColumnDependencyGraph graph)
    {
        var byCode = new Dictionary<string, SalaryColumnDefinition>(StringComparer.OrdinalIgnoreCase);
        var formulas = new Dictionary<string, FormulaNode?>(StringComparer.OrdinalIgnoreCase);

        // Giữ thứ tự DisplayOrder khi đưa vào graph để topo ổn định.
        foreach (var col in columns.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id))
        {
            if (!byCode.TryAdd(col.ColumnCode, col))
                throw new InvalidOperationException($"Trùng mã cột '{col.ColumnCode}' trong bảng công thức.");

            FormulaNode? node = null;
            if (col.ValueSource == ColumnValueSource.Formula && !string.IsNullOrWhiteSpace(col.FormulaJson))
                node = PayrollJson.Deserialize<FormulaNode>(col.FormulaJson);
            formulas[col.ColumnCode] = node;
        }

        var order = graph.TopologicalOrder(formulas);
        var ordered = order.Select(code => byCode[code]).ToList();

        return new PayrollColumnPlan { OrderedColumns = ordered, ByCode = byCode, Formulas = formulas };
    }
}
