namespace MPS.Application.Common.Payroll.Formula;

/// <summary>
/// Cây giải thích cách tính 1 ô — cho HR hover xem "vì sao ra số này". Phản chiếu AST với giá
/// trị + nhãn đã resolve. Vd COM: Detail="10 (Công tính cơm) × 20.000 (Giá cơm/bữa)", Value=200000.
/// </summary>
public sealed class ExplanationNode
{
    /// <summary>Loại nút: number/text/column/field/param/binary/compare/if/function.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Nhãn dễ đọc cho nút lá (tên cột/field/param).</summary>
    public string? Label { get; set; }

    public decimal? Value { get; set; }
    public string? Text { get; set; }

    /// <summary>Chuỗi giải thích 1 dòng cho nút này (đã có giá trị + nhãn con).</summary>
    public string Detail { get; set; } = string.Empty;

    public List<ExplanationNode> Children { get; set; } = new();

    public static ExplanationNode Leaf(string kind, decimal? value, string? label, string? text = null) => new()
    {
        Kind = kind,
        Value = value,
        Label = label,
        Text = text,
        Detail = text ?? (value.HasValue ? PayrollNumberFormat.Format(value.Value) + (label is null ? "" : $" ({label})") : ""),
    };
}
