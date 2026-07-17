using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using MPS.Application.Common.Payroll.Excel;
using MPS.Domain.Enums.Payroll;
using L = MPS.Application.Common.Payroll.Excel.PayrollExcelLayout;
using S = MPS.Infrastructure.Services.Payroll.PayrollExcelStyles;
using C = MPS.Infrastructure.Services.Payroll.PayrollExcelCells;

namespace MPS.Infrastructure.Services.Payroll;

/// <summary>
/// Dựng sheet "4. LUONG" — clone bố cục file mẫu (DAC_TA_BANG_LUONG.md §2): tiêu đề 1–4,
/// TỔNG CỘNG hàng 6, nhãn cột hàng 7, nhóm cột cha hàng 8 (dưới nhãn con — đúng như file gốc),
/// tham số hàng 9–10, số thứ tự cột hàng 11, tên field kỹ thuật hàng 12, dữ liệu từ hàng 13.
/// </summary>
internal static class PayrollExcelSalarySheet
{
    public static Worksheet Build(PayrollExcelBook book)
    {
        var sheetData = new SheetData();
        var constantsByCell = book.Constants
            .Where(k => k.SheetName == L.SheetSalary)
            .ToDictionary(k => k.CellRef, StringComparer.Ordinal);

        AppendTitle(sheetData, book);
        AppendTotalRow(sheetData, book);
        AppendHeaderRows(sheetData, book);
        AppendParamRows(sheetData, book, constantsByCell);
        AppendMetaRows(sheetData, book);
        AppendDataRows(sheetData, book);

        var worksheet = new Worksheet();
        worksheet.Append(BuildSheetViews());
        worksheet.Append(BuildColumnWidths(book));
        worksheet.Append(sheetData);

        var merges = BuildGroupMerges(book);
        if (merges.Count > 0)
        {
            var mergeCells = new MergeCells { Count = (uint)merges.Count };
            foreach (var m in merges) mergeCells.Append(new MergeCell { Reference = m });
            worksheet.Append(mergeCells);
        }
        return worksheet;
    }

    // ===================== Hàng 1–4: tiêu đề =====================

    private static void AppendTitle(SheetData sd, PayrollExcelBook book)
    {
        sd.Append(C.Row(L.RowTitle, C.Text("A1", book.CompanyName, S.Title)));
        sd.Append(C.Row(L.RowSubTitle,
            C.Text("A2", $"BẢNG LƯƠNG THÁNG {book.PeriodMonth:00}/{book.PeriodYear}", S.Title)));
        sd.Append(C.Row(L.RowPeriod,
            C.Text("A3", $"Công chuẩn: {book.StandardWorkingDays:0.##} công · Trạng thái: {book.Status} · " +
                         $"Số nhân viên: {book.Rows.Count}", S.Meta)));
    }

    // ===================== Hàng 6: TỔNG CỘNG =====================

    private static void AppendTotalRow(SheetData sd, PayrollExcelBook book)
    {
        var cells = new List<Cell>();
        for (var i = 0; i < book.Columns.Count; i++)
        {
            var col = book.Columns[i];
            var cell = book.TotalCells[i];
            var reference = $"{col.Letter}{L.RowTotal}";

            if (cell.Text is not null) { cells.Add(C.Text(reference, cell.Text, S.TextTotal)); continue; }
            if (cell.Formula is not null) { cells.Add(C.Formula(reference, cell.Formula, cell.Number, S.MoneyTotal)); continue; }
            cells.Add(new Cell { CellReference = reference, StyleIndex = S.TextTotal });
        }
        sd.Append(C.Row(L.RowTotal, cells.ToArray()));
    }

    // ===================== Hàng 7–8: header 2 tầng =====================

    private static void AppendHeaderRows(SheetData sd, PayrollExcelBook book)
    {
        sd.Append(C.Row(L.RowColumnLabel, book.Columns
            .Select(c => C.Text($"{c.Letter}{L.RowColumnLabel}", c.Label, S.Header)).ToArray()));

        sd.Append(C.Row(L.RowGroupLabel, book.Columns
            .Select(c => C.Text($"{c.Letter}{L.RowGroupLabel}", c.GroupName, S.GroupHeader)).ToArray()));
    }

    /// <summary>Gộp ô nhóm cha ở hàng 8 theo các dải cột liên tiếp cùng GroupName.</summary>
    private static List<string> BuildGroupMerges(PayrollExcelBook book)
    {
        var merges = new List<string>();
        var i = 0;
        while (i < book.Columns.Count)
        {
            var group = book.Columns[i].GroupName;
            if (string.IsNullOrEmpty(group)) { i++; continue; }

            var j = i;
            while (j + 1 < book.Columns.Count && book.Columns[j + 1].GroupName == group) j++;
            if (j > i) merges.Add($"{book.Columns[i].Letter}{L.RowGroupLabel}:{book.Columns[j].Letter}{L.RowGroupLabel}");
            i = j + 1;
        }
        return merges;
    }

    // ===================== Hàng 9–10: tham số =====================

    private static void AppendParamRows(
        SheetData sd, PayrollExcelBook book, IReadOnlyDictionary<string, PayrollExcelConstant> byCell)
    {
        foreach (var row in new[] { L.RowRateParam, L.RowCapParam })
        {
            var cells = book.Columns.Select(col =>
            {
                var reference = $"{col.Letter}{row}";
                if (!byCell.TryGetValue(reference, out var k))
                    return new Cell { CellReference = reference, StyleIndex = S.Default };
                return C.Number(reference, k.Value, k.IsPercent ? S.Percent : S.MoneyParam);
            }).ToArray();
            sd.Append(C.Row((uint)row, cells));
        }
    }

    // ===================== Hàng 11–12: số cột + tên field =====================

    private static void AppendMetaRows(SheetData sd, PayrollExcelBook book)
    {
        sd.Append(C.Row(L.RowColumnNumber, book.Columns
            .Select((c, i) => C.Text($"{c.Letter}{L.RowColumnNumber}", $"[{i + 1}]", S.Meta)).ToArray()));

        sd.Append(C.Row(L.RowFieldName, book.Columns
            .Select(c => C.Text($"{c.Letter}{L.RowFieldName}", c.FieldName, S.Meta)).ToArray()));
    }

    // ===================== Hàng 13+: dữ liệu =====================

    private static void AppendDataRows(SheetData sd, PayrollExcelBook book)
    {
        for (var r = 0; r < book.Rows.Count; r++)
        {
            var excelRow = (uint)(L.RowFirstData + r);
            var cells = new List<Cell>(book.Columns.Count);

            for (var i = 0; i < book.Columns.Count; i++)
            {
                var col = book.Columns[i];
                var cell = book.Rows[r].Cells[i];
                var reference = $"{col.Letter}{excelRow}";
                var style = StyleFor(col, cell);

                if (cell.Text is not null) cells.Add(C.Text(reference, cell.Text, style));
                else if (cell.Formula is not null) cells.Add(C.Formula(reference, cell.Formula, cell.Number, style));
                else cells.Add(C.Number(reference, cell.Number, style));
            }
            sd.Append(C.Row(excelRow, cells.ToArray()));
        }
    }

    private static uint StyleFor(PayrollExcelColumn col, PayrollExcelCell cell)
    {
        if (cell.IsOverridden) return S.MoneyOverridden;
        return col.DataType switch
        {
            ColumnDataType.Money => S.Money,
            ColumnDataType.Text => S.Text,
            _ => S.Number,
        };
    }

    // ===================== Khung nhìn + độ rộng cột =====================

    private static SheetViews BuildSheetViews() => new(
        new SheetView { WorkbookViewId = 0U, TabSelected = true }.AppendPane());

    private static Columns BuildColumnWidths(PayrollExcelBook book)
    {
        var columns = new Columns();
        columns.Append(new Column { Min = 1U, Max = 1U, Width = 5D, CustomWidth = true });

        foreach (var c in book.Columns.Skip(1))
        {
            var width = c.DataType switch
            {
                ColumnDataType.Money => 14D,
                ColumnDataType.Text => 18D,
                _ => 10D,
            };
            columns.Append(new Column
            {
                Min = (uint)c.Index,
                Max = (uint)c.Index,
                Width = width,
                CustomWidth = true,
            });
        }
        return columns;
    }

    /// <summary>Đóng băng tiêu đề (12 hàng đầu) + 2 cột đầu để cuộn ngang vẫn thấy mã/tên NV.</summary>
    private static SheetView AppendPane(this SheetView view)
    {
        view.Append(new Pane
        {
            HorizontalSplit = 2D,
            VerticalSplit = L.RowFieldName,
            TopLeftCell = $"C{L.RowFirstData}",
            ActivePane = PaneValues.BottomRight,
            State = PaneStateValues.Frozen,
        });
        return view;
    }
}
