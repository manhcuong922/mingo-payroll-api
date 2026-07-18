using System.Text.RegularExpressions;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Payroll.Formula;

/// <summary>
/// Kiểm tra whitelist + cấu trúc AST + chu trình. Stateless → đăng ký singleton.
/// KHÔNG tự bịa: field/hàm phải nằm trong danh mục đã đăng ký; ref cột phải tồn tại trong bảng.
/// </summary>
public sealed partial class PayrollFormulaValidator : IPayrollFormulaValidator
{
    private static readonly HashSet<string> BinaryOps = new(StringComparer.Ordinal) { "+", "-", "*", "/" };
    private static readonly HashSet<string> CompareOps = new(StringComparer.Ordinal) { "=", "<>", "<", "<=", ">", ">=" };

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex ColumnCodePattern();

    private readonly IPayrollFieldCatalog _catalog;
    private readonly IFormulaFunctionRegistry _functions;
    private readonly IColumnDependencyGraph _graph;

    public PayrollFormulaValidator(
        IPayrollFieldCatalog catalog, IFormulaFunctionRegistry functions, IColumnDependencyGraph graph)
    {
        _catalog = catalog;
        _functions = functions;
        _graph = graph;
    }

    public IReadOnlyList<string> Validate(IReadOnlyList<SalaryColumnDefinition> columns)
    {
        var errors = new List<string>();
        var knownCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ===== Mã cột: hợp lệ + không trùng =====
        foreach (var c in columns)
        {
            if (string.IsNullOrWhiteSpace(c.ColumnCode))
            {
                errors.Add($"Cột '{c.Label}' thiếu mã cột.");
                continue;
            }
            if (!ColumnCodePattern().IsMatch(c.ColumnCode))
                errors.Add($"Mã cột '{c.ColumnCode}' không hợp lệ (chỉ chữ/số/gạch dưới, bắt đầu bằng chữ).");
            if (!knownCodes.Add(c.ColumnCode))
                errors.Add($"Mã cột '{c.ColumnCode}' bị trùng trong bảng.");
        }

        // ===== Từng cột theo nguồn giá trị =====
        var formulasByCode = new Dictionary<string, FormulaNode?>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in columns)
        {
            if (string.IsNullOrWhiteSpace(c.ColumnCode)) continue;

            switch (c.ValueSource)
            {
                case ColumnValueSource.Lookup:
                    if (string.IsNullOrWhiteSpace(c.LookupKey))
                        errors.Add($"Cột '{c.ColumnCode}' (Lookup) thiếu khoá field.");
                    else if (!_catalog.TryGet(c.LookupKey, out _))
                        errors.Add($"Cột '{c.ColumnCode}' tham chiếu field không tồn tại: '{c.LookupKey}'.");
                    formulasByCode[c.ColumnCode] = null;
                    break;

                case ColumnValueSource.Formula:
                    FormulaNode? node = null;
                    if (string.IsNullOrWhiteSpace(c.FormulaJson))
                    {
                        errors.Add($"Cột '{c.ColumnCode}' (Formula) thiếu biểu thức.");
                    }
                    else
                    {
                        try { node = PayrollJson.Deserialize<FormulaNode>(c.FormulaJson); }
                        catch { errors.Add($"Cột '{c.ColumnCode}' có JSON công thức sai định dạng."); }
                        if (node is null)
                            errors.Add($"Cột '{c.ColumnCode}' có công thức rỗng/không đọc được.");
                        else
                            ValidateNode(node, c.ColumnCode, knownCodes, errors);
                    }
                    formulasByCode[c.ColumnCode] = node;
                    break;

                default: // Input
                    formulasByCode[c.ColumnCode] = null;
                    break;
            }
        }

        // ===== Chu trình phụ thuộc giữa các cột =====
        if (errors.Count == 0 && !_graph.TryTopologicalOrder(formulasByCode, out _, out var cycleError))
            errors.Add(cycleError ?? "Công thức có phụ thuộc vòng (chu trình).");

        return errors;
    }

    private void ValidateNode(
        FormulaNode node, string ownerCode, HashSet<string> knownCodes, List<string> errors)
    {
        switch (node.Type)
        {
            case FormulaNodeType.Number:
                if (node.Number is null) errors.Add($"Cột '{ownerCode}': hằng số thiếu giá trị.");
                break;

            case FormulaNodeType.Text:
                if (node.Text is null) errors.Add($"Cột '{ownerCode}': chuỗi hằng thiếu giá trị.");
                break;

            case FormulaNodeType.Column:
                if (string.IsNullOrWhiteSpace(node.Text))
                    errors.Add($"Cột '{ownerCode}': tham chiếu cột trống.");
                else if (!knownCodes.Contains(node.Text))
                    errors.Add($"Cột '{ownerCode}' tham chiếu cột không tồn tại: '{node.Text}'.");
                break;

            case FormulaNodeType.Field:
                if (string.IsNullOrWhiteSpace(node.Text) || !_catalog.TryGet(node.Text!, out _))
                    errors.Add($"Cột '{ownerCode}' tham chiếu field không tồn tại: '{node.Text}'.");
                break;

            case FormulaNodeType.Binary:
                if (!BinaryOps.Contains(node.Text ?? ""))
                    errors.Add($"Cột '{ownerCode}': phép toán không hợp lệ '{node.Text}'.");
                RequireArity(node, 2, ownerCode, "phép toán 2 ngôi", errors);
                RecurseArgs(node, ownerCode, knownCodes, errors);
                break;

            case FormulaNodeType.Compare:
                if (!CompareOps.Contains(node.Text ?? ""))
                    errors.Add($"Cột '{ownerCode}': phép so sánh không hợp lệ '{node.Text}'.");
                RequireArity(node, 2, ownerCode, "phép so sánh", errors);
                RecurseArgs(node, ownerCode, knownCodes, errors);
                break;

            case FormulaNodeType.If:
                RequireArity(node, 3, ownerCode, "IF(điều kiện, đúng, sai)", errors);
                RecurseArgs(node, ownerCode, knownCodes, errors);
                break;

            case FormulaNodeType.Function:
                if (string.IsNullOrWhiteSpace(node.Text) || !_functions.TryGet(node.Text!, out var fn))
                {
                    errors.Add($"Cột '{ownerCode}' dùng hàm không tồn tại: '{node.Text}'.");
                }
                else if (fn.FixedArity is int arity && node.Args.Count != arity)
                {
                    errors.Add($"Cột '{ownerCode}': hàm {fn.Name} cần {arity} tham số, đang có {node.Args.Count}.");
                }
                RecurseArgs(node, ownerCode, knownCodes, errors);
                break;

            default:
                errors.Add($"Cột '{ownerCode}': loại nút công thức không hỗ trợ.");
                break;
        }
    }

    private static void RequireArity(FormulaNode node, int arity, string ownerCode, string what, List<string> errors)
    {
        if (node.Args.Count != arity)
            errors.Add($"Cột '{ownerCode}': {what} cần {arity} thành phần, đang có {node.Args.Count}.");
    }

    private void RecurseArgs(
        FormulaNode node, string ownerCode, HashSet<string> knownCodes, List<string> errors)
    {
        foreach (var arg in node.Args)
            ValidateNode(arg, ownerCode, knownCodes, errors);
    }
}
