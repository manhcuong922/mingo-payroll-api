using MPS.Application.Common.Models.Document;

namespace MPS.Application.Common.Interfaces;

public interface IDocumentRenderingService
{
    /// <summary>
    /// Merge <paramref name="templateDocx"/> (DOCX stream) với placeholder context,
    /// trả về stream DOCX mới đã thay thế tất cả {{key}} bằng giá trị tương ứng.
    /// </summary>
    Task<Stream> RenderAsync(Stream templateDocx, PlaceholderContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bản nhận thẳng từ điển placeholder — dùng cho biểu mẫu có tập trường riêng, VD tờ khai
    /// quyết toán thuế TNCN với 39 chỉ tiêu <c>{{k19}}</c>..<c>{{k57}}</c>.
    ///
    /// <para>Không nhồi các trường đó vào <see cref="PlaceholderContext"/> vì đó là record
    /// positional 26 tham số dành riêng cho hợp đồng lao động — thêm trường là vỡ mọi nơi dựng nó.</para>
    /// </summary>
    Task<Stream> RenderAsync(
        Stream templateDocx,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);
}
