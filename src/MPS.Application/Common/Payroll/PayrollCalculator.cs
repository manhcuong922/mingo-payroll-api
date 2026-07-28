using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Payroll;

/// <inheritdoc />
public sealed class PayrollCalculator : IPayrollCalculator
{
    private readonly IFormulaEvaluator _evaluator;
    private readonly IColumnDependencyGraph _graph;

    public PayrollCalculator(IFormulaEvaluator evaluator, IColumnDependencyGraph graph)
    {
        _evaluator = evaluator;
        _graph = graph;
    }

    public void ComputeAll(PayrollColumnPlan plan, PayrollCalcContext ctx, PayrollRow row)
    {
        row.Cells.Clear();
        var columnValues = new Dictionary<string, ResolvedValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var col in plan.OrderedColumns)
        {
            var cell = new PayrollCellValue { ColumnCode = col.ColumnCode };

            if (col.ValueSource == ColumnValueSource.Formula)
            {
                var rv = EvaluateFormula(plan, ctx, columnValues, col, cell);
                columnValues[col.ColumnCode] = rv;
            }
            else
            {
                var rv = SeedFromField(col, ctx);
                ApplyToCell(cell, col, rv);
                cell.ExplanationJson = PayrollJson.Serialize(LeafExplanation(col, rv));
                columnValues[col.ColumnCode] = rv;
            }

            row.Cells.Add(cell);
        }
    }

    public void Recompute(PayrollColumnPlan plan, PayrollCalcContext ctx, PayrollRow row, string changedColumnCode)
    {
        var cellByCode = row.Cells.ToDictionary(c => c.ColumnCode, StringComparer.OrdinalIgnoreCase);
        var columnValues = new Dictionary<string, ResolvedValue>(StringComparer.OrdinalIgnoreCase);

        // Nạp giá trị hiệu lực hiện có (kể cả override) làm nền.
        foreach (var col in plan.OrderedColumns)
        {
            columnValues[col.ColumnCode] = cellByCode.TryGetValue(col.ColumnCode, out var existing)
                ? EffectiveResolved(existing, col)
                : new ResolvedValue(0m, col.Label);
        }

        foreach (var code in _graph.DownstreamOrder(changedColumnCode, plan.Formulas))
        {
            if (!plan.ByCode.TryGetValue(code, out var col) || col.ValueSource != ColumnValueSource.Formula) continue;
            if (!cellByCode.TryGetValue(code, out var cell) || cell.IsOverridden) continue; // giữ ô ghim tay

            var rv = EvaluateFormula(plan, ctx, columnValues, col, cell);
            columnValues[code] = rv;
        }
    }

    public void ComputeTotals(PayrollColumnPlan plan, IReadOnlyList<PayrollRow> employeeRows, PayrollRow totalRow)
    {
        totalRow.Cells.Clear();
        foreach (var col in plan.OrderedColumns)
        {
            if (!col.IsMoney) continue;
            decimal sum = 0m;
            foreach (var row in employeeRows)
            {
                var cell = row.Cells.FirstOrDefault(c => string.Equals(c.ColumnCode, col.ColumnCode, StringComparison.OrdinalIgnoreCase));
                if (cell is not null) sum += EffectiveNumber(cell);
            }
            totalRow.Cells.Add(new PayrollCellValue { ColumnCode = col.ColumnCode, ComputedValue = sum });
        }
    }

    // ===== helpers =====

    private ResolvedValue EvaluateFormula(
        PayrollColumnPlan plan, PayrollCalcContext ctx,
        IReadOnlyDictionary<string, ResolvedValue> columnValues,
        SalaryColumnDefinition col, PayrollCellValue cell)
    {
        var node = plan.Formulas[col.ColumnCode];
        if (node is null)
        {
            cell.ComputedValue = 0m;
            return new ResolvedValue(0m, col.Label);
        }

        var evalCtx = new FormulaEvaluationContext
        {
            Columns = columnValues,
            Fields = ctx.Fields,
            FunctionContext = ctx.FunctionContext,
        };
        var result = _evaluator.Evaluate(node, evalCtx);
        var rounded = PayrollRounding.ApplyForColumn(result.Value, col.Rounding, col.DataType);

        cell.ComputedValue = rounded;
        cell.FormulaSnapshotJson = col.FormulaJson;
        cell.ExplanationJson = PayrollJson.Serialize(WrapRounding(result.Explanation, rounded, col));

        return new ResolvedValue(rounded, col.Label);
    }

    private static ExplanationNode WrapRounding(ExplanationNode inner, decimal rounded, SalaryColumnDefinition col)
    {
        if (col.Rounding == RoundingMode.None || (inner.Value.HasValue && inner.Value.Value == rounded))
            return inner;
        return new ExplanationNode
        {
            Kind = "rounding",
            Value = rounded,
            Label = col.Label,
            Children = { inner },
            Detail = $"Làm tròn: {inner.Detail} → {PayrollNumberFormat.Format(rounded)}",
        };
    }

    private static ResolvedValue SeedFromField(SalaryColumnDefinition col, PayrollCalcContext ctx)
    {
        if (col.DataType == ColumnDataType.Text)
        {
            var text = col.LookupKey is not null && ctx.Fields.TryGetValue(col.LookupKey, out var f) ? f.Text ?? string.Empty : string.Empty;
            return new ResolvedValue(0m, col.Label, text);
        }
        var val = col.LookupKey is not null && ctx.Fields.TryGetValue(col.LookupKey, out var fn) ? fn.Value : 0m;
        return new ResolvedValue(val, col.Label);
    }

    private static void ApplyToCell(PayrollCellValue cell, SalaryColumnDefinition col, ResolvedValue rv)
    {
        if (col.DataType == ColumnDataType.Text) cell.TextValue = rv.Text;
        else cell.ComputedValue = rv.Value;
    }

    private static ExplanationNode LeafExplanation(SalaryColumnDefinition col, ResolvedValue rv)
    {
        var kind = col.ValueSource == ColumnValueSource.Input ? "input" : "lookup";
        return ExplanationNode.Leaf(kind, rv.IsText ? null : rv.Value, col.Label, rv.Text);
    }

    private static ResolvedValue EffectiveResolved(PayrollCellValue cell, SalaryColumnDefinition col)
    {
        if (col.DataType == ColumnDataType.Text)
            return new ResolvedValue(0m, col.Label, cell.TextValue ?? string.Empty);
        return new ResolvedValue(EffectiveNumber(cell), col.Label);
    }

    private static decimal EffectiveNumber(PayrollCellValue cell)
        => cell.IsOverridden ? cell.OverrideValue ?? 0m : cell.ComputedValue ?? 0m;
}
