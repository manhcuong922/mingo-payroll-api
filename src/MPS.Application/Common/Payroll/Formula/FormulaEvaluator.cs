namespace MPS.Application.Common.Payroll.Formula;

/// <summary>
/// Đánh giá AST đệ quy, đồng thời dựng cây <see cref="ExplanationNode"/> (giải thích cách tính).
/// Chia 0 → 0 (an toàn khi công chuẩn = 0). Tham chiếu cột/field/param thiếu → ném lỗi rõ ràng.
/// </summary>
public sealed class FormulaEvaluator : IFormulaEvaluator
{
    private readonly IFormulaFunctionRegistry _functions;

    public FormulaEvaluator(IFormulaFunctionRegistry functions) => _functions = functions;

    public EvaluationResult Evaluate(FormulaNode node, FormulaEvaluationContext ctx) => node.Type switch
    {
        FormulaNodeType.Number => Number(node),
        FormulaNodeType.Text => TextNode(node),
        FormulaNodeType.Column => ResolveRef("column", node.Text, ctx.Columns),
        FormulaNodeType.Field => ResolveRef("field", node.Text, ctx.Fields),
        FormulaNodeType.Binary => Binary(node, ctx),
        FormulaNodeType.Compare => Compare(node, ctx),
        FormulaNodeType.If => IfNode(node, ctx),
        FormulaNodeType.Function => Function(node, ctx),
        _ => throw new InvalidOperationException($"Loại nút công thức không hỗ trợ: {node.Type}."),
    };

    private static EvaluationResult Number(FormulaNode node)
    {
        var v = node.Number ?? 0m;
        return new(v, ExplanationNode.Leaf("number", v, null));
    }

    private static EvaluationResult TextNode(FormulaNode node)
    {
        var t = node.Text ?? string.Empty;
        return new(0m, new ExplanationNode { Kind = "text", Text = t, Detail = $"\"{t}\"" }, t);
    }

    private static EvaluationResult ResolveRef(string kind, string? key, IReadOnlyDictionary<string, ResolvedValue> map)
    {
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException($"Nút {kind} thiếu khoá tham chiếu.");
        if (!map.TryGetValue(key, out var rv))
            throw new InvalidOperationException($"Công thức tham chiếu {kind} '{key}' không tồn tại trong ngữ cảnh.");
        var expl = ExplanationNode.Leaf(kind, rv.IsText ? null : rv.Value, rv.Label, rv.Text);
        return new(rv.Value, expl, rv.Text);
    }

    private EvaluationResult Binary(FormulaNode node, FormulaEvaluationContext ctx)
    {
        var l = Evaluate(node.Args[0], ctx);
        var r = Evaluate(node.Args[1], ctx);
        var val = node.Text switch
        {
            "+" => l.Value + r.Value,
            "-" => l.Value - r.Value,
            "*" => l.Value * r.Value,
            "/" => r.Value == 0m ? 0m : l.Value / r.Value,
            _ => throw new InvalidOperationException($"Toán tử không hỗ trợ: {node.Text}."),
        };
        var sym = node.Text switch { "*" => "×", "/" => "÷", _ => node.Text! };
        var expl = new ExplanationNode
        {
            Kind = "binary",
            Value = val,
            Children = { l.Explanation, r.Explanation },
            Detail = $"{l.Explanation.Detail} {sym} {r.Explanation.Detail} = {PayrollNumberFormat.Format(val)}",
        };
        return new(val, expl);
    }

    private EvaluationResult Compare(FormulaNode node, FormulaEvaluationContext ctx)
    {
        var l = Evaluate(node.Args[0], ctx);
        var r = Evaluate(node.Args[1], ctx);
        bool result;
        if (l.Text is not null || r.Text is not null)
        {
            var ls = l.Text ?? PayrollNumberFormat.Format(l.Value);
            var rs = r.Text ?? PayrollNumberFormat.Format(r.Value);
            result = node.Text switch
            {
                "=" => string.Equals(ls, rs, StringComparison.Ordinal),
                "<>" => !string.Equals(ls, rs, StringComparison.Ordinal),
                _ => false,
            };
        }
        else
        {
            result = node.Text switch
            {
                "=" => l.Value == r.Value,
                "<>" => l.Value != r.Value,
                "<" => l.Value < r.Value,
                "<=" => l.Value <= r.Value,
                ">" => l.Value > r.Value,
                ">=" => l.Value >= r.Value,
                _ => throw new InvalidOperationException($"Toán tử so sánh không hỗ trợ: {node.Text}."),
            };
        }
        var val = result ? 1m : 0m;
        var expl = new ExplanationNode
        {
            Kind = "compare",
            Value = val,
            Children = { l.Explanation, r.Explanation },
            Detail = $"{l.Explanation.Detail} {node.Text} {r.Explanation.Detail} → {(result ? "đúng" : "sai")}",
        };
        return new(val, expl);
    }

    private EvaluationResult IfNode(FormulaNode node, FormulaEvaluationContext ctx)
    {
        var cond = Evaluate(node.Args[0], ctx);
        var chosen = cond.IsTruthy ? node.Args[1] : node.Args[2];
        var res = Evaluate(chosen, ctx);
        var expl = new ExplanationNode
        {
            Kind = "if",
            Value = res.Value,
            Text = res.Text,
            Children = { cond.Explanation, res.Explanation },
            Detail = $"Nếu [{cond.Explanation.Detail}] → {res.Explanation.Detail}",
        };
        return new(res.Value, expl, res.Text);
    }

    private EvaluationResult Function(FormulaNode node, FormulaEvaluationContext ctx)
    {
        if (string.IsNullOrEmpty(node.Text) || !_functions.TryGet(node.Text, out var fn))
            throw new InvalidOperationException($"Hàm không tồn tại: {node.Text}.");

        var argResults = node.Args.Select(a => Evaluate(a, ctx)).ToList();
        if (fn.FixedArity is int arity && argResults.Count != arity)
            throw new InvalidOperationException($"Hàm {fn.Name} cần {arity} tham số, nhận {argResults.Count}.");

        var fr = fn.Invoke(argResults.Select(a => a.Value).ToList(), ctx.FunctionContext);
        var expl = new ExplanationNode
        {
            Kind = "function",
            Label = fn.Name,
            Value = fr.Value,
            Children = argResults.Select(a => a.Explanation).ToList(),
            Detail = $"{fn.Name}: {fr.Explanation}",
        };
        return new(fr.Value, expl);
    }
}
