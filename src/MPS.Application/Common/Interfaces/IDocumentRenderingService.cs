using MPS.Application.Common.Models.Document;

namespace MPS.Application.Common.Interfaces;

public interface IDocumentRenderingService
{
    /// <summary>
    /// Merge <paramref name="templateDocx"/> (DOCX stream) với placeholder context,
    /// trả về stream DOCX mới đã thay thế tất cả {{key}} bằng giá trị tương ứng.
    /// </summary>
    Task<Stream> RenderAsync(Stream templateDocx, PlaceholderContext context, CancellationToken cancellationToken = default);
}
