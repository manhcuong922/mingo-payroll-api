using DocumentFormat.OpenXml.Spreadsheet;

namespace MPS.Infrastructure.Services.AttendanceSheet;

/// <summary>
/// Stylesheet cho bảng chấm công Excel. Thứ tự CellFormat trong array = style index dùng ở exporter.
/// </summary>
internal static class AttendanceSheetStyles
{
    public const uint Default = 0;
    public const uint Bold = 1;
    public const uint Title = 2;
    public const uint Header = 3;          // header thường (fill xanh nhạt)
    public const uint HeaderWeekend = 4;   // header cột T7/CN (fill vàng nhạt)
    public const uint BodyCenter = 5;      // ô body căn giữa
    public const uint BodyLeft = 6;        // ô họ tên / chức vụ căn trái
    public const uint BodyBold = 7;        // ô TC + Tổng công (bold)
    public const uint BodyWeekend = 8;      // ô body ngày cuối tuần (fill vàng nhạt)
    public const uint NoteGrayBlock = 9;    // ô trống trong bảng chú thích (chỉ fill xám)
    public const uint NoteTitleGray = 10;   // "Chú thích ký hiệu:" title — bold + fill xám
    public const uint NoteSymbolGray = 11;  // col B ký hiệu (X/P/V…) — bold center + fill xám
    public const uint NoteMeaningGray = 12; // col C meaning — left align + fill xám

    public static Stylesheet Build() => new(
        new Fonts(
            new Font(),                                                                // 0 thường
            new Font(new Bold()),                                                      // 1 đậm
            new Font(new Bold(), new FontSize { Val = 14D })),                         // 2 tiêu đề
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),            // 0 (bắt buộc)
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),         // 1 (bắt buộc)
            Solid("FFD9E1F2"),                                                         // 2 header xanh nhạt
            Solid("FFFFF2CC"),                                                         // 3 weekend vàng nhạt
            Solid("FF808080")),                                                        // 4 gray cho bảng chú thích
        new Borders(
            new Border(),                                                              // 0 không viền
            new Border(                                                                // 1 viền mảnh
                new LeftBorder(new Color { Auto = true }) { Style = BorderStyleValues.Thin },
                new RightBorder(new Color { Auto = true }) { Style = BorderStyleValues.Thin },
                new TopBorder(new Color { Auto = true }) { Style = BorderStyleValues.Thin },
                new BottomBorder(new Color { Auto = true }) { Style = BorderStyleValues.Thin },
                new DiagonalBorder())),
        new CellFormats(
            new CellFormat(),                                                          // 0 Default
            new CellFormat { FontId = 1, ApplyFont = true },                           // 1 Bold
            new CellFormat                                                              // 2 Title
            {
                FontId = 2, ApplyFont = true,
                Alignment = new Alignment
                {
                    Horizontal = HorizontalAlignmentValues.Center,
                    Vertical = VerticalAlignmentValues.Center,
                },
                ApplyAlignment = true,
            },
            HeaderFormat(fillId: 2),                                                   // 3 Header
            HeaderFormat(fillId: 3),                                                   // 4 HeaderWeekend
            BodyFormat(HorizontalAlignmentValues.Center, fillId: 0),                   // 5 BodyCenter
            BodyFormat(HorizontalAlignmentValues.Left, fillId: 0),                     // 6 BodyLeft
            BodyFormat(HorizontalAlignmentValues.Center, fillId: 0, fontId: 1),        // 7 BodyBold
            BodyFormat(HorizontalAlignmentValues.Center, fillId: 3),                   // 8 BodyWeekend
            new CellFormat { FillId = 4, ApplyFill = true },                           // 9 NoteGrayBlock (chỉ fill)
            new CellFormat                                                              // 10 NoteTitleGray
            {
                FontId = 1, FillId = 4, ApplyFont = true, ApplyFill = true,
            },
            NoteCellFormat(HorizontalAlignmentValues.Center, fontId: 1),               // 11 NoteSymbolGray (không border)
            NoteCellFormat(HorizontalAlignmentValues.Left)));                          // 12 NoteMeaningGray (không border)

    private static Fill Solid(string rgb) =>
        new(new PatternFill(new ForegroundColor { Rgb = rgb }) { PatternType = PatternValues.Solid });

    private static CellFormat HeaderFormat(uint fillId) => new()
    {
        FontId = 1, FillId = fillId, BorderId = 1,
        ApplyFont = true, ApplyFill = true, ApplyBorder = true,
        Alignment = new Alignment
        {
            WrapText = true,
            Horizontal = HorizontalAlignmentValues.Center,
            Vertical = VerticalAlignmentValues.Center,
        },
        ApplyAlignment = true,
    };

    private static CellFormat BodyFormat(HorizontalAlignmentValues horizontal, uint fillId, uint fontId = 0) => new()
    {
        FontId = fontId, FillId = fillId, BorderId = 1,
        ApplyFont = fontId != 0, ApplyFill = fillId != 0, ApplyBorder = true,
        Alignment = new Alignment
        {
            WrapText = true,
            Horizontal = horizontal,
            Vertical = VerticalAlignmentValues.Center,
        },
        ApplyAlignment = true,
    };

    /// <summary>Cell format cho bảng chú thích — fill xám #808080, KHÔNG border.</summary>
    private static CellFormat NoteCellFormat(HorizontalAlignmentValues horizontal, uint fontId = 0) => new()
    {
        FontId = fontId, FillId = 4,
        ApplyFont = fontId != 0, ApplyFill = true,
        Alignment = new Alignment
        {
            WrapText = true,
            Horizontal = horizontal,
            Vertical = VerticalAlignmentValues.Center,
        },
        ApplyAlignment = true,
    };
}
