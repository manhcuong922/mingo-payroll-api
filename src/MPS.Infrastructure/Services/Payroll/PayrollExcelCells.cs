using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace MPS.Infrastructure.Services.Payroll;

/// <summary>
/// Tiện ích dựng ô OpenXml. Lưu ý thứ tự con của &lt;c&gt; theo schema là f → v → is: luôn set
/// <see cref="Cell.CellFormula"/> TRƯỚC <see cref="Cell.CellValue"/>, nếu không file sẽ hỏng.
/// </summary>
internal static class PayrollExcelCells
{
    public static Cell Text(string reference, string? text, uint style = PayrollExcelStyles.Text)
    {
        var cell = new Cell { CellReference = reference, StyleIndex = style };
        if (string.IsNullOrEmpty(text)) return cell;
        cell.DataType = CellValues.InlineString;
        cell.InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return cell;
    }

    public static Cell Number(string reference, decimal? value, uint style)
    {
        var cell = new Cell { CellReference = reference, StyleIndex = style };
        if (value is null) return cell;
        cell.DataType = CellValues.Number;
        cell.CellValue = new CellValue(value.Value.ToString(CultureInfo.InvariantCulture));
        return cell;
    }

    /// <summary>Ghi CẢ công thức lẫn giá trị cache: mở file thấy số đúng ngay, bấm vào ô thấy công thức.</summary>
    public static Cell Formula(string reference, string formula, decimal? cached, uint style)
    {
        var cell = new Cell { CellReference = reference, StyleIndex = style };
        cell.CellFormula = new CellFormula(formula);
        if (cached is not null)
        {
            cell.DataType = CellValues.Number;
            cell.CellValue = new CellValue(cached.Value.ToString(CultureInfo.InvariantCulture));
        }
        return cell;
    }

    public static Row Row(uint index, params Cell[] cells)
    {
        var row = new Row { RowIndex = index };
        row.Append(cells);
        return row;
    }

    /// <summary>"V9" → (row 9, col 22).</summary>
    public static (uint Row, uint Column) Parse(string cellRef)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        var digits = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        var col = letters.Aggregate(0, (acc, c) => acc * 26 + (c - 'A' + 1));
        return (uint.Parse(digits, CultureInfo.InvariantCulture), (uint)col);
    }
}
