using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace MPS.Infrastructure.Services.Payroll;

/// <summary>
/// Gắn comment (hover) vào ô. Excel yêu cầu BA thứ đi kèm nhau: phần comments (nội dung), phần VML
/// legacy (hình khung chú thích) và thẻ &lt;legacyDrawing&gt; trên worksheet — thiếu 1 là comment
/// không hiện. VML phải ghi thô vì SDK không có class typed cho nó.
/// </summary>
internal static class PayrollExcelComments
{
    public sealed record Note(string CellRef, string Text);

    private const string Author = "Mingo Payroll System";

    public static void Attach(WorksheetPart wsPart, IReadOnlyList<Note> notes)
    {
        if (notes.Count == 0) return;

        var commentList = new CommentList();
        foreach (var n in notes)
        {
            commentList.Append(new Comment
            {
                Reference = n.CellRef,
                AuthorId = 0U,
                CommentText = new CommentText(
                    new Run(new Text(n.Text) { Space = SpaceProcessingModeValues.Preserve })),
            });
        }

        var commentsPart = wsPart.AddNewPart<WorksheetCommentsPart>();
        commentsPart.Comments = new Comments(new Authors(new Author(Author)), commentList);
        commentsPart.Comments.Save();

        var vmlPart = wsPart.AddNewPart<VmlDrawingPart>();
        using (var stream = vmlPart.GetStream(FileMode.Create))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(BuildVml(notes));
        }

        // Phải nằm sau mergeCells theo thứ tự schema của worksheet.
        wsPart.Worksheet.Append(new LegacyDrawing { Id = wsPart.GetIdOfPart(vmlPart) });
    }

    private static string BuildVml(IReadOnlyList<Note> notes)
    {
        var sb = new StringBuilder();
        sb.Append(
            "<xml xmlns:v=\"urn:schemas-microsoft-com:vml\" " +
            "xmlns:o=\"urn:schemas-microsoft-com:office:office\" " +
            "xmlns:x=\"urn:schemas-microsoft-com:office:excel\">" +
            "<o:shapelayout v:ext=\"edit\"><o:idmap v:ext=\"edit\" data=\"1\"/></o:shapelayout>" +
            "<v:shapetype id=\"_x0000_t202\" coordsize=\"21600,21600\" o:spt=\"202\" " +
            "path=\"m,l,21600r21600,l21600,xe\">" +
            "<v:stroke joinstyle=\"miter\"/>" +
            "<v:path gradientshapeok=\"t\" o:connecttype=\"rect\"/>" +
            "</v:shapetype>");

        for (var i = 0; i < notes.Count; i++)
        {
            var (row, column) = PayrollExcelCells.Parse(notes[i].CellRef);
            sb.Append(
                $"<v:shape id=\"_x0000_s{1025 + i}\" type=\"#_x0000_t202\" " +
                $"style=\"position:absolute;width:340pt;height:120pt;z-index:{i + 1};visibility:hidden\" " +
                "fillcolor=\"#ffffe1\" o:insetmode=\"auto\">" +
                "<v:fill color2=\"#ffffe1\"/>" +
                "<v:shadow on=\"t\" color=\"black\" obscured=\"t\"/>" +
                "<v:path o:connecttype=\"none\"/>" +
                "<v:textbox style=\"mso-direction-alt:auto\"/>" +
                "<x:ClientData ObjectType=\"Note\">" +
                "<x:MoveWithCells/><x:SizeWithCells/><x:AutoFill>False</x:AutoFill>" +
                $"<x:Row>{row - 1}</x:Row><x:Column>{column - 1}</x:Column>" +
                "</x:ClientData></v:shape>");
        }

        sb.Append("</xml>");
        return sb.ToString();
    }
}
