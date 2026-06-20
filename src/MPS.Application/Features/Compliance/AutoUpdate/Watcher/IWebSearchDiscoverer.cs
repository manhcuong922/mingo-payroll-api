namespace MPS.Application.Features.Compliance.AutoUpdate.Watcher;

/// <summary>Văn bản chi phối mới hơn mà LLM phát hiện (chưa kiểm chứng — chỉ là "chỉ điểm").</summary>
public sealed record DiscoveryResult(
    string? DocumentNo,
    DateOnly? EffectiveDate,
    string? SourceUrl,
    string Summary);

/// <summary>Ném khi web search không khả dụng (proxy/tool không hỗ trợ, lỗi gọi) → caller degrade sang NeedsHuman.</summary>
public sealed class WebSearchUnavailableException : Exception
{
    public WebSearchUnavailableException(string message) : base(message) { }
}

/// <summary>
/// Kênh B — hỏi LLM (kèm web search) xem có văn bản chi phối MỚI HƠN cho một loại entity không.
/// Trả null nếu không có bản mới; throw <see cref="WebSearchUnavailableException"/> nếu không tra được.
/// </summary>
public interface IWebSearchDiscoverer
{
    Task<DiscoveryResult?> DiscoverNewerAsync(string targetEntityType, string? currentDocumentNo, CancellationToken ct);
}
