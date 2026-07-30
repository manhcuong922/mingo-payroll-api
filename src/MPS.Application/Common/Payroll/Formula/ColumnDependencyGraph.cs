namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Kahn topo-sort giữ thứ tự đầu vào (DisplayOrder) cho ổn định; phát hiện chu trình.</summary>
public sealed class ColumnDependencyGraph : IColumnDependencyGraph
{
    public IReadOnlyList<string> ExtractColumnRefs(FormulaNode? formula)
    {
        if (formula is null) return Array.Empty<string>();
        var refs = new List<string>();
        Collect(formula, refs);
        return refs;
    }

    private static void Collect(FormulaNode node, List<string> acc)
    {
        if (node.Type == FormulaNodeType.Column && !string.IsNullOrEmpty(node.Text))
        {
            if (!acc.Contains(node.Text)) acc.Add(node.Text);
        }
        foreach (var child in node.Args) Collect(child, acc);
    }

    public IReadOnlyList<string> TopologicalOrder(IReadOnlyDictionary<string, FormulaNode?> columnFormulas)
    {
        if (!TryTopologicalOrder(columnFormulas, out var order, out var error))
            throw new InvalidOperationException(error);
        return order;
    }

    public bool TryTopologicalOrder(
        IReadOnlyDictionary<string, FormulaNode?> columnFormulas,
        out IReadOnlyList<string> order,
        out string? error)
    {
        var nodes = columnFormulas.Keys.ToList(); // giữ thứ tự chèn = DisplayOrder
        var known = nodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // deps[c] = các cột c phụ thuộc (chỉ giữ ref hợp lệ trong tập cột).
        var deps = new Dictionary<string, List<string>>();
        foreach (var c in nodes)
        {
            var refs = ExtractColumnRefs(columnFormulas[c])
                .Where(r => known.Contains(r) && !string.Equals(r, c, StringComparison.OrdinalIgnoreCase))
                .ToList();
            deps[c] = refs;
        }

        var indegree = nodes.ToDictionary(c => c, c => deps[c].Count, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var ready = new Queue<string>(nodes.Where(c => indegree[c] == 0));

        while (ready.Count > 0)
        {
            var c = ready.Dequeue();
            result.Add(c);
            foreach (var other in nodes)
            {
                if (!deps[other].Contains(c, StringComparer.OrdinalIgnoreCase)) continue;
                indegree[other]--;
                if (indegree[other] == 0) ready.Enqueue(other);
            }
        }

        if (result.Count != nodes.Count)
        {
            var cyclic = nodes.Where(c => !result.Contains(c, StringComparer.OrdinalIgnoreCase));
            order = Array.Empty<string>();
            error = $"Công thức có phụ thuộc vòng (chu trình) giữa các cột: {string.Join(", ", cyclic)}.";
            return false;
        }

        order = result;
        error = null;
        return true;
    }

    public IReadOnlyList<string> DownstreamOrder(string changedColumn, IReadOnlyDictionary<string, FormulaNode?> columnFormulas)
    {
        var full = TopologicalOrder(columnFormulas);

        // dependents[x] = cột phụ thuộc trực tiếp x.
        var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in columnFormulas.Keys) dependents[c] = new List<string>();
        foreach (var c in columnFormulas.Keys)
        {
            foreach (var r in ExtractColumnRefs(columnFormulas[c]))
                if (dependents.ContainsKey(r)) dependents[r].Add(c);
        }

        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { changedColumn };
        var stack = new Stack<string>();
        stack.Push(changedColumn);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!dependents.TryGetValue(cur, out var list)) continue;
            foreach (var d in list)
                if (affected.Add(d)) stack.Push(d);
        }

        return full.Where(c => affected.Contains(c)).ToList();
    }
}
