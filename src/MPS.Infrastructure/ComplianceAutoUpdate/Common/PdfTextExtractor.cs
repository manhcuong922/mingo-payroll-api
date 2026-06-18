using System.Text;
using UglyToad.PdfPig;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Common;

/// <summary>Rút text từ PDF (toàn văn công báo là PDF ký số có lớp text).</summary>
public static class PdfTextExtractor
{
    public static string Extract(byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0) return string.Empty;
        try
        {
            using var doc = PdfDocument.Open(pdfBytes);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            return sb.ToString();
        }
        catch
        {
            // PDF mã hoá / scan ảnh / hỏng → trả rỗng, caller xử lý graceful.
            return string.Empty;
        }
    }
}
