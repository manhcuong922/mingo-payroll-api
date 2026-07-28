using DocumentFormat.OpenXml.Spreadsheet;
using MPS.Application.Common.Payroll.Excel;
using L = MPS.Application.Common.Payroll.Excel.PayrollExcelLayout;
using S = MPS.Infrastructure.Services.Payroll.PayrollExcelStyles;
using C = MPS.Infrastructure.Services.Payroll.PayrollExcelCells;

namespace MPS.Infrastructure.Services.Payroll;

/// <summary>Sheet "THAM SO" (hằng số + biểu thuế + căn cứ pháp lý) và "GHI CHU" (trần/ngưỡng + đổi luật).</summary>
internal static class PayrollExcelInfoSheets
{
    // ===================== THAM SO =====================

    public static Worksheet BuildParams(PayrollExcelBook book)
    {
        var sd = new SheetData();

        sd.Append(C.Row(1, C.Text("A1", $"THAM SỐ ÁP DỤNG — KỲ {book.PeriodMonth:00}/{book.PeriodYear}", S.Title)));
        sd.Append(C.Row(2,
            C.Text("A2", "Tham số", S.Header),
            C.Text("B2", "Tên dùng trong công thức", S.Header),
            C.Text("C2", "Giá trị", S.Header),
            C.Text("D2", "Vị trí trên bảng lương", S.Header)));

        for (var i = 0; i < book.Constants.Count; i++)
        {
            var k = book.Constants[i];
            var row = (uint)(L.RowFirstConstant + i);
            var valueStyle = k.IsPercent ? S.Percent : S.MoneyParam;

            // Hằng số neo trên sheet lương → ô này soi chiếu (live) tới ô gốc, sửa 1 chỗ là đổi cả hai.
            var valueCell = k.SheetName == L.SheetSalary
                ? C.Formula($"C{row}", k.AbsoluteRef, k.Value, valueStyle)
                : C.Number($"C{row}", k.Value, valueStyle);

            sd.Append(C.Row(row,
                C.Text($"A{row}", k.Label),
                C.Text($"B{row}", k.Name),
                valueCell,
                C.Text($"D{row}", k.SheetName == L.SheetSalary ? $"'{k.SheetName}'!{k.CellRef}" : "(chỉ ở sheet này)")));
        }

        var next = (uint)(L.RowFirstConstant + book.Constants.Count + 1);
        next = AppendPitBrackets(sd, book, next);
        AppendComplianceRefs(sd, book, next + 1);

        return Sheet(sd, widths: new[] { 34D, 24D, 16D, 26D, 30D, 18D });
    }

    private static uint AppendPitBrackets(SheetData sd, PayrollExcelBook book, uint row)
    {
        sd.Append(C.Row(row, C.Text($"A{row}", "BIỂU THUẾ TNCN LUỸ TIẾN TỪNG PHẦN (snapshot của kỳ)", S.Bold)));
        row++;
        sd.Append(C.Row(row,
            C.Text($"A{row}", "Bậc", S.Header),
            C.Text($"B{row}", "TNTT từ (>)", S.Header),
            C.Text($"C{row}", "TNTT đến (≤)", S.Header),
            C.Text($"D{row}", "Thuế suất", S.Header)));
        row++;

        foreach (var b in book.PitBrackets.OrderBy(b => b.Order))
        {
            sd.Append(C.Row(row,
                C.Number($"A{row}", b.Order, S.Number),
                C.Number($"B{row}", b.LowerBound, S.Money),
                b.UpperBound is null ? C.Text($"C{row}", "trở lên") : C.Number($"C{row}", b.UpperBound, S.Money),
                C.Number($"D{row}", b.Rate, S.Percent)));
            row++;
        }
        return row;
    }

    private static void AppendComplianceRefs(SheetData sd, PayrollExcelBook book, uint row)
    {
        sd.Append(C.Row(row, C.Text($"A{row}", "CĂN CỨ PHÁP LÝ ÁP DỤNG CHO KỲ", S.Bold)));
        row++;
        sd.Append(C.Row(row,
            C.Text($"A{row}", "Loại", S.Header),
            C.Text($"B{row}", "Chỉ số", S.Header),
            C.Text($"C{row}", "Giá trị", S.Header),
            C.Text($"D{row}", "Hiệu lực từ", S.Header),
            C.Text($"E{row}", "Căn cứ pháp lý", S.Header),
            C.Text($"F{row}", "Version", S.Header)));
        row++;

        foreach (var r in book.ComplianceRefs)
        {
            sd.Append(C.Row(row,
                C.Text($"A{row}", r.VersionType),
                C.Text($"B{row}", r.HeadlineLabel),
                C.Number($"C{row}", r.HeadlineValue, S.Money),
                C.Text($"D{row}", r.ValidFrom.ToString("dd/MM/yyyy")),
                C.Text($"E{row}", r.LegalReference),
                C.Number($"F{row}", r.VersionId, S.Number)));
            row++;
        }
    }

    // ===================== GHI CHU =====================

    public static Worksheet BuildNotes(PayrollExcelBook book)
    {
        var sd = new SheetData();

        sd.Append(C.Row(1, C.Text("A1", $"GHI CHÚ BẢNG LƯƠNG — KỲ {book.PeriodMonth:00}/{book.PeriodYear}", S.Title)));
        sd.Append(C.Row(2, C.Text("A2",
            "Trần/ngưỡng áp dụng và thay đổi pháp lý so với kỳ trước (từ số → đến số, kèm căn cứ).", S.Meta)));

        sd.Append(C.Row(4,
            C.Text("A4", "Loại", S.Header),
            C.Text("B4", "Cột liên quan", S.Header),
            C.Text("C4", "Tiêu đề", S.Header),
            C.Text("D4", "Nội dung", S.Header),
            C.Text("E4", "Từ", S.Header),
            C.Text("F4", "Đến", S.Header),
            C.Text("G4", "Căn cứ pháp lý", S.Header),
            C.Text("H4", "Nguồn", S.Header)));

        var row = 5U;
        foreach (var n in book.Notes)
        {
            sd.Append(C.Row(row,
                C.Text($"A{row}", NoteTypeLabel(n.NoteType)),
                C.Text($"B{row}", n.ColumnCode ?? "(cả bảng)"),
                C.Text($"C{row}", n.Title),
                C.Text($"D{row}", n.Body),
                C.Number($"E{row}", n.OldValue, S.Money),
                C.Number($"F{row}", n.NewValue, S.Money),
                C.Text($"G{row}", n.LegalReference),
                C.Text($"H{row}", n.AutoGenerated ? "Tự sinh" : "Kế toán nhập")));
            row++;
        }

        if (book.Notes.Count == 0)
        {
            sd.Append(C.Row(row, C.Text($"A{row}", "(Kỳ này không có ghi chú)", S.Meta)));
            row++;
        }

        AppendFlagLegend(sd, row + 2);

        return Sheet(sd, widths: new[] { 16D, 18D, 34D, 70D, 16D, 16D, 30D, 14D });
    }

    /// <summary>Chú giải ý nghĩa từng mã cờ BH + cờ thuế (2 cột hệ thống suy từ HĐ — chỉ đọc).</summary>
    private static void AppendFlagLegend(SheetData sd, uint row)
    {
        sd.Append(C.Row(row, C.Text($"A{row}",
            "CHÚ GIẢI CỜ BẢO HIỂM & CỜ THUẾ (2 cột này hệ thống suy từ hợp đồng — chỉ đọc, không sửa tay)", S.Bold)));
        row += 2;

        row = AppendFlagBlock(sd, row, "Cờ bảo hiểm (cột \"Cờ BH\")",
            MPS.Domain.Enums.Payroll.PayrollFlagLegend.Insurance);
        row += 1;
        AppendFlagBlock(sd, row, "Cờ thuế (cột \"Cờ thuế\")",
            MPS.Domain.Enums.Payroll.PayrollFlagLegend.Tax);
    }

    private static uint AppendFlagBlock(
        SheetData sd, uint row, string title,
        IReadOnlyList<MPS.Domain.Enums.Payroll.PayrollFlagLegend.Entry> entries)
    {
        sd.Append(C.Row(row, C.Text($"A{row}", title, S.Bold)));
        row++;
        sd.Append(C.Row(row,
            C.Text($"A{row}", "Mã", S.Header),
            C.Text($"B{row}", "Loại", S.Header),
            C.Text($"C{row}", "Ý nghĩa", S.Header)));
        row++;

        foreach (var e in entries)
        {
            sd.Append(C.Row(row,
                C.Text($"A{row}", MPS.Domain.Enums.Payroll.PayrollFlagLegend.DisplayCode(e.Code)),
                C.Text($"B{row}", e.Label),
                C.Text($"C{row}", e.Description)));
            row++;
        }
        return row;
    }

    private static string NoteTypeLabel(string noteType) => noteType switch
    {
        nameof(Domain.Enums.Payroll.PayrollNoteType.CapThreshold) => "Trần/ngưỡng",
        nameof(Domain.Enums.Payroll.PayrollNoteType.LawChange) => "Thay đổi luật",
        nameof(Domain.Enums.Payroll.PayrollNoteType.AllowanceLimit) => "Giới hạn phụ cấp",
        nameof(Domain.Enums.Payroll.PayrollNoteType.Manual) => "Ghi chú tay",
        _ => noteType,
    };

    private static Worksheet Sheet(SheetData sd, double[] widths)
    {
        var columns = new Columns();
        for (var i = 0; i < widths.Length; i++)
            columns.Append(new Column
            {
                Min = (uint)(i + 1), Max = (uint)(i + 1), Width = widths[i], CustomWidth = true,
            });

        var ws = new Worksheet();
        ws.Append(columns);
        ws.Append(sd);
        return ws;
    }
}
