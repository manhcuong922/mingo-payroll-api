namespace MPS.Application.Features.Compliance.AutoUpdate.Ingestion;

/// <summary>
/// Phiên âm PDF scan/ảnh thành text thuần bằng Claude vision — fallback cho Kênh A khi
/// <c>PdfTextExtractor</c> không bóc được lớp text. Trả rỗng nếu không đọc được, vượt cap trang,
/// hoặc OCR chưa bật.
/// </summary>
public interface IVisionTranscriber
{
    Task<string> TranscribePdfAsync(byte[] pdfBytes, CancellationToken ct);
}
