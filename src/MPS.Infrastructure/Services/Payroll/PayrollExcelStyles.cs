using DocumentFormat.OpenXml.Spreadsheet;

namespace MPS.Infrastructure.Services.Payroll;

/// <summary>Bảng style cho file lương + chỉ số style để dùng ở exporter (thứ tự phải khớp CellFormats).</summary>
internal static class PayrollExcelStyles
{
    public const uint Default = 0;
    public const uint Bold = 1;
    public const uint Title = 2;
    public const uint Header = 3;
    public const uint GroupHeader = 4;
    public const uint Money = 5;
    public const uint MoneyTotal = 6;
    public const uint MoneyOverridden = 7;
    public const uint MoneyParam = 8;
    public const uint Percent = 9;
    public const uint Number = 10;
    public const uint Text = 11;
    public const uint Meta = 12;
    public const uint TextTotal = 13;

    private const uint FmtMoney = 164;
    private const uint FmtNumber = 165;
    private const uint FmtPercent = 166;

    public static Stylesheet Build() => new(
        new NumberingFormats(
            new NumberingFormat { NumberFormatId = FmtMoney, FormatCode = "#,##0" },
            new NumberingFormat { NumberFormatId = FmtNumber, FormatCode = "#,##0.##" },
            new NumberingFormat { NumberFormatId = FmtPercent, FormatCode = "0.00%" }),
        new Fonts(
            new Font(),                                                                    // 0 thường
            new Font(new Bold()),                                                          // 1 đậm
            new Font(new Bold(), new FontSize { Val = 14D }),                              // 2 tiêu đề
            // Thứ tự con của <font> theo schema là b → i → sz → color; sai thứ tự Excel báo hỏng file.
            new Font(new Italic(), new FontSize { Val = 8D },                              // 3 phụ (hàng [n], tên field)
                new Color { Rgb = "FF808080" })),
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),                // 0 (bắt buộc)
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),             // 1 (bắt buộc)
            Solid("FFDCE6F1"),                                                             // 2 header
            Solid("FFFFF2CC"),                                                             // 3 ô sửa tay
            Solid("FFEAF1DD"),                                                             // 4 tham số
            Solid("FFF2DCDB")),                                                            // 5 dòng tổng
        new Borders(
            new Border(),                                                                  // 0 không viền
            new Border(                                                                    // 1 viền mảnh
                new LeftBorder(new Color { Auto = true }) { Style = BorderStyleValues.Thin },
                new RightBorder(new Color { Auto = true }) { Style = BorderStyleValues.Thin },
                new TopBorder(new Color { Auto = true }) { Style = BorderStyleValues.Thin },
                new BottomBorder(new Color { Auto = true }) { Style = BorderStyleValues.Thin },
                new DiagonalBorder())),
        new CellFormats(
            new CellFormat(),                                                              // 0 Default
            new CellFormat { FontId = 1, ApplyFont = true },                               // 1 Bold
            new CellFormat { FontId = 2, ApplyFont = true },                               // 2 Title
            new CellFormat                                                                 // 3 Header
            {
                FontId = 1, FillId = 2, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true,
                Alignment = new Alignment
                {
                    WrapText = true,
                    Horizontal = HorizontalAlignmentValues.Center,
                    Vertical = VerticalAlignmentValues.Center,
                },
                ApplyAlignment = true,
            },
            new CellFormat                                                                 // 4 GroupHeader
            {
                FontId = 1, FillId = 2, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true,
                Alignment = new Alignment
                {
                    Horizontal = HorizontalAlignmentValues.Center,
                    Vertical = VerticalAlignmentValues.Center,
                    WrapText = true,
                },
                ApplyAlignment = true,
            },
            Num(FmtMoney, borderId: 1),                                                    // 5 Money
            Num(FmtMoney, borderId: 1, fontId: 1, fillId: 5),                              // 6 MoneyTotal
            Num(FmtMoney, borderId: 1, fillId: 3),                                         // 7 MoneyOverridden
            Num(FmtMoney, borderId: 1, fillId: 4, fontId: 1),                              // 8 MoneyParam
            Num(FmtPercent, borderId: 1, fillId: 4, fontId: 1),                            // 9 Percent
            Num(FmtNumber, borderId: 1),                                                   // 10 Number
            new CellFormat { BorderId = 1, ApplyBorder = true },                           // 11 Text
            new CellFormat { FontId = 3, ApplyFont = true },                               // 12 Meta
            new CellFormat                                                                 // 13 TextTotal
            {
                FontId = 1, FillId = 5, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true,
            }));

    private static Fill Solid(string rgb) =>
        new(new PatternFill(new ForegroundColor { Rgb = rgb }) { PatternType = PatternValues.Solid });

    private static CellFormat Num(uint numFmt, uint borderId = 0, uint fontId = 0, uint fillId = 0) => new()
    {
        NumberFormatId = numFmt,
        FontId = fontId,
        FillId = fillId,
        BorderId = borderId,
        ApplyNumberFormat = true,
        ApplyFont = fontId != 0,
        ApplyFill = fillId != 0,
        ApplyBorder = borderId != 0,
    };
}
