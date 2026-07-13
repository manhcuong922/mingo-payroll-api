using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Evaluation;
using MPS.Application.Features.Evaluation.OrderEvaluation.Commands;
using MPS.Domain.Exceptions;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Xuất/nhập Excel bảng-theo-đơn bằng DocumentFormat.OpenXml. Mẫu gồm 2 sheet:
///   • "Huong dan": thang điểm 1–5, công thức tính 2 tầng, thông tin đơn.
///   • "Cham diem": bảng dạng dài (mỗi dòng = 1 công nhân × 1 tiêu chí), có cột điểm (DataValidation 1..5),
///     cột ghi chú, và 2 cột ẩn subjectId/lineId để import map lại.
/// Xem plan Phần 2.
/// </summary>
public sealed class OrderEvaluationExcelService : IOrderEvaluationExcelService
{
    private const string ScoringSheetName = "Cham diem";
    private const string GuideSheetName = "Huong dan";

    // Chỉ số cột (0-based) của sheet chấm điểm.
    private const int ColSubjectId = 0;   // A (ẩn)
    private const int ColLineId = 1;      // B (ẩn)
    private const int ColEmployee = 2;    // C
    private const int ColGroup = 3;       // D
    private const int ColGroupWeight = 4; // E
    private const int ColMetric = 5;      // F
    private const int ColMetricWeight = 6;// G
    private const int ColScore = 7;       // H (nhập)
    private const int ColNote = 8;        // I (nhập)
    private const int ColCount = 9;

    // ===================== EXPORT =====================

    public byte[] BuildTemplate(OrderEvaluationDto e)
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
        {
            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new Workbook();

            var stylesPart = wbPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = BuildStylesheet();
            stylesPart.Stylesheet.Save();

            var sheets = wbPart.Workbook.AppendChild(new Sheets());

            // Sheet 1 — Hướng dẫn
            var guidePart = wbPart.AddNewPart<WorksheetPart>();
            guidePart.Worksheet = new Worksheet(BuildGuideSheetData(e));
            sheets.Append(new Sheet { Id = wbPart.GetIdOfPart(guidePart), SheetId = 1, Name = GuideSheetName });

            // Sheet 2 — Chấm điểm
            var scorePart = wbPart.AddNewPart<WorksheetPart>();
            scorePart.Worksheet = BuildScoringWorksheet(e);
            sheets.Append(new Sheet { Id = wbPart.GetIdOfPart(scorePart), SheetId = 2, Name = ScoringSheetName });

            wbPart.Workbook.Save();
        }
        return ms.ToArray();
    }

    private static SheetData BuildGuideSheetData(OrderEvaluationDto e)
    {
        var sd = new SheetData();
        void Title(string t) => sd.Append(Row(TextCell(t, 1)));
        void Line(string t) => sd.Append(Row(TextCell(t)));

        Title("BẢNG ĐÁNH GIÁ THEO ĐƠN — MẪU CHẤM ĐIỂM");
        Line($"Đơn hàng: {e.ProjectTitle ?? $"#{e.ProjectId}"}");
        Line($"Mã bảng: #{e.Id}    Kỳ: {e.PeriodMonth:00}/{e.PeriodYear}    Người chấm: {e.EvaluatorName ?? $"#{e.EvaluatorUserId}"}");
        Line("");
        Title("THANG ĐIỂM (1–5)");
        Line("5 — Xuất sắc: vượt yêu cầu, không có lỗi.");
        Line("4 — Tốt: đạt yêu cầu, lỗi nhỏ không đáng kể.");
        Line("3 — Đạt: hoàn thành cơ bản, còn vài điểm cần cải thiện.");
        Line("2 — Cần cải thiện: chưa đạt một số yêu cầu quan trọng.");
        Line("1 — Kém: không đạt yêu cầu.");
        Line("");
        Title("CÁCH TÍNH ĐIỂM (trọng số 2 tầng)");
        Line("Điểm nhóm = Σ(Trọng số tiêu chí × Điểm) / Σ(Trọng số tiêu chí) — trong từng nhóm.");
        Line("Điểm tổng = Σ(Trọng số nhóm × Điểm nhóm) / Σ(Trọng số nhóm).");
        Line("Bỏ trống ô điểm nếu chưa chấm; ô để trống sẽ KHÔNG được nhập khi upload.");
        Line("");
        Title("HƯỚNG DẪN");
        Line("1. Sang sheet \"Cham diem\", nhập điểm 1–5 vào cột \"Điểm (1-5)\" cho từng công nhân theo từng tiêu chí.");
        Line("2. Có thể ghi chú ở cột \"Ghi chú\".");
        Line("3. KHÔNG sửa/xoá 2 cột ẩn đầu (mã đối chiếu) và không đổi thứ tự dòng.");
        Line("4. Lưu file rồi upload lại ở màn hình Bảng theo đơn để chấm hàng loạt.");
        AssignReferences(sd);
        return sd;
    }

    private static Worksheet BuildScoringWorksheet(OrderEvaluationDto e)
    {
        var sd = new SheetData();

        // Header (style 2).
        sd.Append(Row(
            TextCell("subjectId", 2), TextCell("lineId", 2), TextCell("Công nhân", 2),
            TextCell("Nhóm", 2), TextCell("Trọng số nhóm (%)", 2), TextCell("Tiêu chí", 2),
            TextCell("Trọng số tiêu chí (%)", 2), TextCell("Điểm (1-5)", 2), TextCell("Ghi chú", 2)));

        uint rowIdx = 1;
        foreach (var s in e.Subjects)
        {
            foreach (var l in s.Scores)
            {
                rowIdx++;
                var cells = new Cell[ColCount];
                cells[ColSubjectId] = NumberCell(s.Id);
                cells[ColLineId] = NumberCell(l.Id);
                cells[ColEmployee] = TextCell(s.EmployeeName ?? $"#{s.EmployeeUserId}");
                cells[ColGroup] = TextCell(l.TypeNameSnapshot, 3);
                cells[ColGroupWeight] = NumberCell(l.TypeWeightSnapshot);
                cells[ColMetric] = TextCell(l.MetricNameSnapshot, 3);
                cells[ColMetricWeight] = NumberCell(l.MetricWeightSnapshot);
                cells[ColScore] = l.Score.HasValue ? NumberCell(l.Score.Value) : EmptyCell();
                cells[ColNote] = TextCell(l.Note ?? string.Empty);
                sd.Append(Row(cells));
            }
        }

        var lastRow = rowIdx;
        AssignReferences(sd);

        // Cột: ẩn A,B; đặt độ rộng vài cột cho dễ đọc.
        var cols = new Columns(
            new Column { Min = 1, Max = 2, Hidden = true, CustomWidth = true, Width = 10 },   // A,B ẩn
            new Column { Min = 3, Max = 3, CustomWidth = true, Width = 22 },                    // Công nhân
            new Column { Min = 4, Max = 4, CustomWidth = true, Width = 22 },                    // Nhóm
            new Column { Min = 5, Max = 5, CustomWidth = true, Width = 16 },
            new Column { Min = 6, Max = 6, CustomWidth = true, Width = 30 },                    // Tiêu chí
            new Column { Min = 7, Max = 7, CustomWidth = true, Width = 18 },
            new Column { Min = 8, Max = 8, CustomWidth = true, Width = 10 },                    // Điểm
            new Column { Min = 9, Max = 9, CustomWidth = true, Width = 28 });                   // Ghi chú

        var ws = new Worksheet();
        ws.Append(cols);
        ws.Append(sd);

        // DataValidation 1..5 cho cột Điểm (H2:H{last}).
        if (lastRow >= 2)
        {
            var scoreColLetter = ColumnLetter(ColScore);
            var dv = new DataValidation
            {
                Type = DataValidationValues.Whole,
                Operator = DataValidationOperatorValues.Between,
                AllowBlank = true,
                ShowErrorMessage = true,
                ErrorTitle = "Điểm không hợp lệ",
                Error = "Điểm phải là số nguyên từ 1 đến 5 (hoặc để trống nếu chưa chấm).",
                SequenceOfReferences = new ListValue<StringValue> { InnerText = $"{scoreColLetter}2:{scoreColLetter}{lastRow}" },
            };
            dv.Append(new Formula1("1"));
            dv.Append(new Formula2("5"));
            ws.Append(new DataValidations(dv) { Count = 1 });
        }

        return ws;
    }

    // ===================== IMPORT =====================

    public IReadOnlyList<ScoreSubjectInput> ParseScores(Stream xlsx)
    {
        SpreadsheetDocument doc;
        try
        {
            doc = SpreadsheetDocument.Open(xlsx, false);
        }
        catch (Exception)
        {
            throw new DomainValidationException("File không phải Excel (.xlsx) hợp lệ.");
        }

        using (doc)
        {
            var wbPart = doc.WorkbookPart
                ?? throw new DomainValidationException("File Excel không hợp lệ (thiếu workbook).");

            var sheet = wbPart.Workbook.Descendants<Sheet>()
                .FirstOrDefault(s => s.Name == ScoringSheetName)
                ?? throw new DomainValidationException($"Không tìm thấy sheet \"{ScoringSheetName}\" trong file. Vui lòng dùng đúng file mẫu.");

            var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
            var sharedStrings = wbPart.SharedStringTablePart?.SharedStringTable;

            var rows = wsPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>() ?? Enumerable.Empty<Row>();

            var bySubject = new Dictionary<int, List<ScoreLineInput>>();

            foreach (var row in rows)
            {
                if (row.RowIndex is not null && row.RowIndex.Value == 1) continue; // header

                var byCol = new Dictionary<int, string>();
                var pos = 0;
                foreach (var cell in row.Elements<Cell>())
                {
                    // Ưu tiên CellReference (Excel luôn ghi); fallback vị trí nếu thiếu.
                    var colIdx = ColumnIndex(cell.CellReference?.Value);
                    if (colIdx < 0) colIdx = pos;
                    byCol[colIdx] = GetText(cell, sharedStrings);
                    pos++;
                }

                if (!byCol.TryGetValue(ColSubjectId, out var sidText) || !int.TryParse(sidText, out var subjectId))
                    continue;   // dòng không có mã đối chiếu → bỏ
                if (!byCol.TryGetValue(ColLineId, out var lidText) || !int.TryParse(lidText, out var lineId))
                    continue;

                byCol.TryGetValue(ColScore, out var scoreText);
                if (string.IsNullOrWhiteSpace(scoreText)) continue;   // chưa chấm → bỏ qua dòng

                if (!int.TryParse(scoreText.Trim(), out var score) || score < 1 || score > 5)
                    throw new DomainValidationException(
                        $"Điểm \"{scoreText}\" không hợp lệ (dòng mã {subjectId}/{lineId}). Điểm phải là số nguyên 1..5.");

                byCol.TryGetValue(ColNote, out var note);
                note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

                if (!bySubject.TryGetValue(subjectId, out var lines))
                    bySubject[subjectId] = lines = new List<ScoreLineInput>();
                lines.Add(new ScoreLineInput(lineId, score, note));
            }

            if (bySubject.Count == 0)
                throw new DomainValidationException("File chưa có điểm nào hợp lệ để nhập.");

            return bySubject
                .Select(kv => new ScoreSubjectInput(kv.Key, null, kv.Value))
                .ToList();
        }
    }

    // ===================== HELPERS =====================

    private static Row Row(params Cell[] cells)
    {
        var r = new Row();
        r.Append(cells);
        return r;
    }

    /// <summary>Gán RowIndex + CellReference (A1) theo vị trí — file đọc lại được kể cả khi chưa mở bằng Excel.</summary>
    private static void AssignReferences(SheetData sd)
    {
        uint rowNum = 1;
        foreach (var row in sd.Elements<Row>())
        {
            row.RowIndex = rowNum;
            var col = 0;
            foreach (var cell in row.Elements<Cell>())
            {
                cell.CellReference = ColumnLetter(col) + rowNum;
                col++;
            }
            rowNum++;
        }
    }

    private static Cell TextCell(string text, uint styleIndex = 0) => new()
    {
        DataType = CellValues.InlineString,
        StyleIndex = styleIndex,
        InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve }),
    };

    private static Cell NumberCell(decimal value) => new()
    {
        CellValue = new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
    };

    private static Cell NumberCell(int value) => new() { CellValue = new CellValue(value.ToString()) };

    private static Cell EmptyCell() => new();

    private static string GetText(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (sharedStrings is not null && int.TryParse(cell.CellValue?.InnerText, out var idx))
                return sharedStrings.ElementAtOrDefault(idx)?.InnerText ?? string.Empty;
            return string.Empty;
        }
        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.Text?.Text ?? cell.InlineString?.InnerText ?? string.Empty;
        return cell.CellValue?.InnerText ?? string.Empty;
    }

    /// <summary>"H12" → 7 (0-based). Trả -1 nếu không đọc được.</summary>
    private static int ColumnIndex(string? cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return -1;
        var letters = new StringBuilder();
        foreach (var c in cellRef)
        {
            if (char.IsLetter(c)) letters.Append(char.ToUpperInvariant(c));
            else break;
        }
        if (letters.Length == 0) return -1;

        var idx = 0;
        foreach (var c in letters.ToString())
            idx = idx * 26 + (c - 'A' + 1);
        return idx - 1;
    }

    /// <summary>0 → "A".</summary>
    private static string ColumnLetter(int index)
    {
        var sb = new StringBuilder();
        index++;
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            sb.Insert(0, (char)('A' + rem));
            index = (index - 1) / 26;
        }
        return sb.ToString();
    }

    private static Stylesheet BuildStylesheet() => new(
        new Fonts(
            new Font(),                                   // 0 — thường
            new Font(new Bold())),                        // 1 — đậm
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),      // 0 (reserved)
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),   // 1 (reserved)
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFDCE6F1" }) { PatternType = PatternValues.Solid })), // 2 — header
        new Borders(new Border()),
        new CellFormats(
            new CellFormat(),                                                                        // 0 — mặc định
            new CellFormat { FontId = 1, ApplyFont = true },                                         // 1 — đậm
            new CellFormat { FontId = 1, FillId = 2, ApplyFont = true, ApplyFill = true,             // 2 — header
                Alignment = new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Center } },
            new CellFormat { Alignment = new Alignment { WrapText = true } }));                       // 3 — wrap text
}
