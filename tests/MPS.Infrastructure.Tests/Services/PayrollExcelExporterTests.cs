using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll.Excel;
using MPS.Application.DTOs.Payroll;
using MPS.Infrastructure.Services.Payroll;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Ghi ra .xlsx thật rồi MỞ LẠI để kiểm chứng: có công thức trong ô (không phải chỉ số chết),
/// có named range, có comment ghi chú, và đủ 3 sheet. Bắt lỗi OpenXml (thứ tự phần tử, VML thiếu)
/// mà unit test dựng workbook không thấy được.
/// Ghi chú của kỳ để RIÊNG (sheet GHI CHU + tóm tắt hover ở tiêu đề), không gắn vào cột.
/// </summary>
public sealed class PayrollExcelExporterTests
{
    private static PayrollExcelBook Book(
        IReadOnlyList<PayrollExcelConstant>? constants = null,
        IReadOnlyList<PayrollNoteDto>? notes = null)
    {
        var columns = new List<PayrollExcelColumn>
        {
            new("STT", "STT", null, "A", 1, Domain.Enums.Payroll.ColumnDataType.Number,
                Domain.Enums.Payroll.ColumnValueSource.Formula, Domain.Enums.Payroll.RoundingMode.None, false),
            new("MANV", "Mã NV", "Nhân viên", "B", 2, Domain.Enums.Payroll.ColumnDataType.Text,
                Domain.Enums.Payroll.ColumnValueSource.Lookup, Domain.Enums.Payroll.RoundingMode.None, false),
            new("MUCLUONG", "Mức lương", "Lương", "C", 3, Domain.Enums.Payroll.ColumnDataType.Money,
                Domain.Enums.Payroll.ColumnValueSource.Input, Domain.Enums.Payroll.RoundingMode.None, true),
            new("THUCNHAN", "THỰC NHẬN", "Kết quả cuối", "D", 4, Domain.Enums.Payroll.ColumnDataType.Money,
                Domain.Enums.Payroll.ColumnValueSource.Formula, Domain.Enums.Payroll.RoundingMode.ToThousand, true),
        };

        var rows = new List<PayrollExcelDataRow>
        {
            new(1, new List<PayrollExcelCell>
            {
                new(Number: 1, Formula: "ROW()-12"),
                new(Text: "F5-0001"),
                new(Number: 14_641_000m),
                new(Number: 14_641_000m, Formula: "ROUND(C13,-3)"),
            }),
            new(2, new List<PayrollExcelCell>
            {
                new(Number: 2, Formula: "ROW()-12"),
                new(Text: "F5-0002"),
                new(Number: 7_700_000m),
                new(Number: 7_000_000m, IsOverridden: true, Comment: "Ô đã sửa tay nên không còn công thức."),
            }),
        };

        var totals = new List<PayrollExcelCell>
        {
            new(),
            new(Text: "Cộng"),
            new(Number: 22_341_000m, Formula: "SUM(C13:C14)"),
            new(Number: 21_641_000m, Formula: "SUM(D13:D14)"),
        };

        return new PayrollExcelBook(
            2023, 8, "Draft", 23m,
            columns, rows, totals,
            constants ?? new List<PayrollExcelConstant>
            {
                new("compliance.bhxhBhytCap", "TRAN_BHXH_BHYT", "Trần BHXH/BHYT",
                    29_800_000m, false, PayrollExcelLayout.SheetSalary, "C10"),
                new("MEAL_PRICE_PER_WORKDAY", "GIA_COM", "Đơn giá cơm/công",
                    40_000m, false, PayrollExcelLayout.SheetParams, "C4"),
            },
            new List<PitBracketSnapshot>
            {
                new(1, 0m, 5_000_000m, 0.05m),
                new(2, 5_000_000m, null, 0.10m),
            },
            notes ?? new List<PayrollNoteDto>(),
            new List<PayrollComplianceRefDto>
            {
                new("Pit", 7, new DateOnly(2020, 7, 1), "NQ 954/2020/UBTVQH14", "Giảm trừ bản thân", 11_000_000m),
            });
    }

    private static SpreadsheetDocument Open(byte[] bytes) =>
        SpreadsheetDocument.Open(new MemoryStream(bytes, writable: false), false);

    private static WorksheetPart SheetPart(SpreadsheetDocument doc, string name)
    {
        var sheet = doc.WorkbookPart!.Workbook.Descendants<Sheet>().Single(s => s.Name == name);
        return (WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id!);
    }

    private static Cell? CellAt(WorksheetPart part, string reference) =>
        part.Worksheet.Descendants<Cell>().FirstOrDefault(c => c.CellReference == reference);

    [Fact]
    public void Workbook_passes_OpenXml_schema_validation_so_Excel_opens_it_without_repairing()
    {
        var bytes = new PayrollExcelExporter().Build(Book(
            notes: new List<PayrollNoteDto>
            {
                new(1, "CapThreshold", null, "Trần BHXH", "29.800.000", null, 29_800_000m, "NĐ 38/2022", true),
            }));

        using var doc = Open(bytes);
        var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc).ToList();

        if (errors.Count > 0)
            Assert.Fail("Excel sẽ báo hỏng file. Lỗi schema:\n" +
                string.Join("\n", errors.Select(e => $"  {e.Path?.XPath} → {e.Description}")));
    }

    [Fact]
    public void Produces_a_readable_workbook_with_the_three_expected_sheets()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        doc.WorkbookPart!.Workbook.Descendants<Sheet>().Select(s => s.Name!.Value)
            .Should().Equal(PayrollExcelLayout.SheetSalary, PayrollExcelLayout.SheetParams, PayrollExcelLayout.SheetNotes);
    }

    [Fact]
    public void Formula_cells_carry_both_the_formula_and_the_cached_value()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        var cell = CellAt(SheetPart(doc, PayrollExcelLayout.SheetSalary), "D13")!;

        // Bấm vào ô thấy công thức...
        cell.CellFormula!.Text.Should().Be("ROUND(C13,-3)");
        // ...mà mở file lên vẫn thấy ngay số đúng, không phải 0.
        cell.CellValue!.Text.Should().Be("14641000");
    }

    [Fact]
    public void Overridden_cell_has_no_formula_so_the_manual_number_survives()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        var cell = CellAt(SheetPart(doc, PayrollExcelLayout.SheetSalary), "D14")!;

        cell.CellFormula.Should().BeNull();
        cell.CellValue!.Text.Should().Be("7000000");
    }

    [Fact]
    public void Total_row_sits_above_the_header_at_row_6_like_the_sample()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        var part = SheetPart(doc, PayrollExcelLayout.SheetSalary);

        CellAt(part, "D6")!.CellFormula!.Text.Should().Be("SUM(D13:D14)");
        CellAt(part, "B6")!.InnerText.Should().Contain("Cộng");
    }

    [Fact]
    public void Constants_are_published_as_named_ranges_pointing_at_absolute_cells()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        var names = doc.WorkbookPart!.Workbook.Descendants<DefinedName>()
            .ToDictionary(n => n.Name!.Value!, n => n.Text);

        names.Should().ContainKey("TRAN_BHXH_BHYT");
        names["TRAN_BHXH_BHYT"].Should().Be("'4. LUONG'!$C$10");
        names["GIA_COM"].Should().Be("'THAM SO'!$C$4");
    }

    [Fact]
    public void Workbook_recalculates_on_load_so_cached_values_can_never_drift_from_the_formulas()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        doc.WorkbookPart!.Workbook.Descendants<CalculationProperties>()
            .Single().FullCalculationOnLoad!.Value.Should().BeTrue();
    }

    [Fact]
    public void Table_level_notes_are_hoverable_on_the_period_title_since_they_belong_to_no_column()
    {
        // Note auto do PayrollLawChangeNoteBuilder sinh ra đều KHÔNG có columnCode → nếu chỉ gắn
        // comment theo cột thì trong sheet lương sẽ chẳng hover thấy ghi chú nào.
        var notes = new List<PayrollNoteDto>
        {
            new(1, "CapThreshold", null, "Trần đóng BHXH/BHYT",
                "Trần đóng BHXH/BHYT: 46.800.000 — NĐ 158/2025/NĐ-CP.", null, 46_800_000m, "NĐ 158/2025/NĐ-CP", true),
            new(2, "LawChange", null, "Thay đổi luật: Thuế TNCN",
                "Giảm trừ bản thân: 11.000.000 → 15.500.000.", 11_000_000m, 15_500_000m, "NQ 110/2025", true),
        };
        var bytes = new PayrollExcelExporter().Build(Book(notes: notes));

        using var doc = Open(bytes);
        var comment = SheetPart(doc, PayrollExcelLayout.SheetSalary)
            .WorksheetCommentsPart!.Comments.Descendants<Comment>()
            .Single(c => c.Reference! == $"A{PayrollExcelLayout.RowSubTitle}");

        comment.InnerText.Should().Contain("Trần đóng BHXH/BHYT")
            .And.Contain("11.000.000 → 15.500.000")
            .And.Contain(PayrollExcelLayout.SheetNotes);
    }

    [Fact]
    public void Overridden_cells_get_a_comment_explaining_why_they_lost_their_formula()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        var comments = SheetPart(doc, PayrollExcelLayout.SheetSalary)
            .WorksheetCommentsPart!.Comments.Descendants<Comment>().ToList();

        comments.Single(c => c.Reference! == "D14").InnerText.Should().Contain("sửa tay");
    }

    [Fact]
    public void Params_sheet_mirrors_salary_sheet_constants_and_lists_the_tax_brackets()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        var part = SheetPart(doc, PayrollExcelLayout.SheetParams);

        // Hằng số neo trên sheet lương → ô soi chiếu live, sửa 1 chỗ đổi cả hai.
        CellAt(part, "C3")!.CellFormula!.Text.Should().Be("'4. LUONG'!$C$10");
        // Hằng số không có cột neo → giá trị nằm ngay dòng liệt kê của nó.
        CellAt(part, "C4")!.CellValue!.Text.Should().Be("40000");

        part.Worksheet.InnerText.Should().Contain("BIỂU THUẾ TNCN").And.Contain("NQ 954/2020/UBTVQH14");
    }

    [Fact]
    public void Notes_sheet_spells_out_law_changes_with_old_and_new_values()
    {
        var notes = new List<PayrollNoteDto>
        {
            new(1, "LawChange", "GTBANTHAN", "Thay đổi luật: Thuế TNCN",
                "Giảm trừ bản thân: 9.000.000 → 11.000.000 theo NQ 954/2020/UBTVQH14.",
                9_000_000m, 11_000_000m, "NQ 954/2020/UBTVQH14", true),
        };
        var bytes = new PayrollExcelExporter().Build(Book(notes: notes));

        using var doc = Open(bytes);
        var text = SheetPart(doc, PayrollExcelLayout.SheetNotes).Worksheet.InnerText;

        text.Should().Contain("Thay đổi luật").And.Contain("GTBANTHAN")
            .And.Contain("NQ 954/2020/UBTVQH14").And.Contain("9000000").And.Contain("11000000");
    }

    [Fact]
    public void Empty_notes_still_produce_a_valid_sheet_that_says_so()
    {
        var bytes = new PayrollExcelExporter().Build(Book());

        using var doc = Open(bytes);
        SheetPart(doc, PayrollExcelLayout.SheetNotes).Worksheet.InnerText
            .Should().Contain("không có ghi chú");
    }
}
