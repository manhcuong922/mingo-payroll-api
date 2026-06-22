namespace MPS.Application.Features.Compliance.AutoUpdate.Ingestion;

/// <summary>
/// Rút TOÀN VĂN từ một văn bản do người nạp (Kênh A): file upload hoặc URL.
/// Hỗ trợ PDF (lớp text) + HTML + text thuần. Trả rỗng nếu không đọc được (PDF scan/ảnh, trang JS).
/// </summary>
public interface IDocumentTextFetcher
{
    /// <summary>
    /// Rút text từ bytes file. <paramref name="contentTypeOrName"/> = content-type hoặc tên file để đoán định dạng.
    /// PDF scan/ảnh (PdfPig trả ít text) → fallback OCR bằng vision.
    /// </summary>
    Task<string> FromBytesAsync(byte[] bytes, string contentTypeOrName, CancellationToken ct);

    /// <summary>Tải URL rồi rút text (PDF/HTML). Rỗng nếu lỗi tải/không có text.</summary>
    Task<string> FromUrlAsync(string url, CancellationToken ct);
}
