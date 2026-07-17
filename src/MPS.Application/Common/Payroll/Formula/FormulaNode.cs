namespace MPS.Application.Common.Payroll.Formula;

/// <summary>
/// 1 nút biểu thức. Mô hình 1-type-nhiều-kind để serialize JSON đơn giản (đệ quy qua <see cref="Args"/>).
/// Dùng factory tĩnh để dựng công thức mặc định cho dễ đọc.
/// </summary>
public sealed class FormulaNode
{
    public FormulaNodeType Type { get; set; }
    public decimal? Number { get; set; }
    public string? Text { get; set; }
    public List<FormulaNode> Args { get; set; } = new();

    // ===== Factory helpers =====
    public static FormulaNode Const(decimal value) => new() { Type = FormulaNodeType.Number, Number = value };
    public static FormulaNode Literal(string text) => new() { Type = FormulaNodeType.Text, Text = text };
    public static FormulaNode Col(string columnCode) => new() { Type = FormulaNodeType.Column, Text = columnCode };
    public static FormulaNode Field(string fieldKey) => new() { Type = FormulaNodeType.Field, Text = fieldKey };

    public static FormulaNode Bin(string op, FormulaNode left, FormulaNode right) =>
        new() { Type = FormulaNodeType.Binary, Text = op, Args = { left, right } };

    public static FormulaNode Add(FormulaNode l, FormulaNode r) => Bin("+", l, r);
    public static FormulaNode Sub(FormulaNode l, FormulaNode r) => Bin("-", l, r);
    public static FormulaNode Mul(FormulaNode l, FormulaNode r) => Bin("*", l, r);
    public static FormulaNode Div(FormulaNode l, FormulaNode r) => Bin("/", l, r);

    public static FormulaNode Cmp(string op, FormulaNode left, FormulaNode right) =>
        new() { Type = FormulaNodeType.Compare, Text = op, Args = { left, right } };

    public static FormulaNode If(FormulaNode cond, FormulaNode then, FormulaNode @else) =>
        new() { Type = FormulaNodeType.If, Args = { cond, then, @else } };

    public static FormulaNode Func(string name, params FormulaNode[] args) =>
        new() { Type = FormulaNodeType.Function, Text = name, Args = args.ToList() };
}
