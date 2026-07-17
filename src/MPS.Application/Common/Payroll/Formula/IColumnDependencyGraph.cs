namespace MPS.Application.Common.Payroll.Formula;

/// <summary>
/// Dựng DAG phụ thuộc giữa các cột (từ ColumnRef trong công thức), sắp topo, phát hiện chu trình.
/// </summary>
public interface IColumnDependencyGraph
{
    /// <summary>Trích các mã cột mà 1 công thức tham chiếu trực tiếp.</summary>
    IReadOnlyList<string> ExtractColumnRefs(FormulaNode? formula);

    /// <summary>Sắp topo toàn bộ cột. Ném lỗi nếu có chu trình.</summary>
    IReadOnlyList<string> TopologicalOrder(IReadOnlyDictionary<string, FormulaNode?> columnFormulas);

    /// <summary>Non-throwing — dùng cho validator khi lưu công thức.</summary>
    bool TryTopologicalOrder(IReadOnlyDictionary<string, FormulaNode?> columnFormulas, out IReadOnlyList<string> order, out string? error);

    /// <summary>Cột đã đổi + toàn bộ cột phụ thuộc (transitive) theo thứ tự topo — để recompute.</summary>
    IReadOnlyList<string> DownstreamOrder(string changedColumn, IReadOnlyDictionary<string, FormulaNode?> columnFormulas);
}
