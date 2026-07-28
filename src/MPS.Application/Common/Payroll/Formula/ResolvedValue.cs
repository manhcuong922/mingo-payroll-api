namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Giá trị đã resolve của 1 cột/field/param cùng nhãn hiển thị (để dựng explanation).</summary>
public readonly record struct ResolvedValue(decimal Value, string Label, string? Text = null)
{
    public bool IsText => Text is not null;
}
