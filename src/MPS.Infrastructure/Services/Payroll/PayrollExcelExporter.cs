using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll.Excel;
using L = MPS.Application.Common.Payroll.Excel.PayrollExcelLayout;

namespace MPS.Infrastructure.Services.Payroll;

/// <summary>
/// Render bảng lương ra .xlsx bằng OpenXml: sheet "4. LUONG" (công thức Excel thật + comment ghi chú),
/// "THAM SO" (hằng số + biểu thuế + căn cứ pháp lý), "GHI CHU" (trần/ngưỡng + thay đổi luật).
/// Hằng số của kỳ được đặt tên (named range) nên công thức đọc là <c>=K13*GIA_COM</c>.
/// </summary>
public sealed class PayrollExcelExporter : IPayrollExcelExporter
{
    public byte[] Build(PayrollExcelBook book)
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
        {
            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new Workbook();

            var stylesPart = wbPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = PayrollExcelStyles.Build();
            stylesPart.Stylesheet.Save();

            var sheets = wbPart.Workbook.AppendChild(new Sheets());

            var salaryPart = wbPart.AddNewPart<WorksheetPart>();
            salaryPart.Worksheet = PayrollExcelSalarySheet.Build(book);
            sheets.Append(new Sheet
            {
                Id = wbPart.GetIdOfPart(salaryPart), SheetId = 1U, Name = L.SheetSalary,
            });
            PayrollExcelComments.Attach(salaryPart, CollectNotes(book));

            var paramsPart = wbPart.AddNewPart<WorksheetPart>();
            paramsPart.Worksheet = PayrollExcelInfoSheets.BuildParams(book);
            sheets.Append(new Sheet
            {
                Id = wbPart.GetIdOfPart(paramsPart), SheetId = 2U, Name = L.SheetParams,
            });

            var notesPart = wbPart.AddNewPart<WorksheetPart>();
            notesPart.Worksheet = PayrollExcelInfoSheets.BuildNotes(book);
            sheets.Append(new Sheet
            {
                Id = wbPart.GetIdOfPart(notesPart), SheetId = 3U, Name = L.SheetNotes,
            });

            var names = BuildDefinedNames(book);
            if (names is not null) wbPart.Workbook.Append(names);

            // Buộc Excel tính lại toàn bộ khi mở: giá trị cache và công thức phải khớp nhau.
            wbPart.Workbook.Append(new CalculationProperties { FullCalculationOnLoad = true });
            wbPart.Workbook.Save();
        }
        return ms.ToArray();
    }

    /// <summary>Named range cho từng hằng số → công thức đọc được tên thay vì $AG$10.</summary>
    private static DefinedNames? BuildDefinedNames(PayrollExcelBook book)
    {
        if (book.Constants.Count == 0) return null;

        var names = new DefinedNames();
        foreach (var k in book.Constants)
            names.Append(new DefinedName { Name = k.Name, Text = k.AbsoluteRef });
        return names;
    }

    /// <summary>
    /// Comment trên sheet lương. Ghi chú của kỳ để RIÊNG (sheet GHI CHU + tóm tắt hover ở ô tiêu
    /// đề), KHÔNG gắn vào từng cột — note auto không mang ColumnCode nên gắn theo cột chỉ là đoán.
    /// Riêng ô đã sửa tay vẫn có comment giải thích vì ô đó không còn công thức để bấm vào xem.
    /// </summary>
    private static List<PayrollExcelComments.Note> CollectNotes(PayrollExcelBook book)
    {
        var notes = new List<PayrollExcelComments.Note>();

        if (book.Notes.Count > 0)
        {
            var body = string.Join("\n\n", book.Notes.Select(n => $"• {n.Title}\n  {n.Body}"));
            notes.Add(new PayrollExcelComments.Note(
                $"A{L.RowSubTitle}",
                $"GHI CHÚ ÁP DỤNG CHO KỲ NÀY ({book.Notes.Count}) — chi tiết ở sheet \"{L.SheetNotes}\":\n\n{body}"));
        }

        for (var r = 0; r < book.Rows.Count; r++)
        {
            var excelRow = L.RowFirstData + r;
            for (var i = 0; i < book.Columns.Count; i++)
            {
                var cell = book.Rows[r].Cells[i];
                if (cell.Comment is null) continue;
                notes.Add(new PayrollExcelComments.Note($"{book.Columns[i].Letter}{excelRow}", cell.Comment));
            }
        }

        return notes;
    }
}
