namespace MPS.Application.Common.Options;

/// <summary>
/// Cấu hình OCR CCCD bằng Claude vision (Anthropic Messages API).
/// Thay thế FPT.AI làm provider chính; FPT.AI giữ lại làm fallback (xem <see cref="FallbackToFptAi"/>).
/// </summary>
public sealed class AnthropicOcrOptions
{
    public const string SectionName = "AnthropicOcr";

    /// <summary>Tắt hẳn provider Claude — mọi request rơi thẳng về FPT.AI.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Tên biến môi trường chứa API key — KHÔNG hardcode key vào config.</summary>
    public string ApiKeyEnvVar { get; init; } = "OPERIX_LLM_API_KEY";

    /// <summary>Rỗng = gọi thẳng https://api.anthropic.com. Trỏ CLIProxyAPI thì điền base URL của proxy.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Dùng ID đầy đủ — CLIProxyAPI không nhận alias rút gọn (vd "claude-haiku-4-5").
    /// Opus 5 đọc ảnh ở 2576px cạnh dài, quan trọng với chữ nhỏ + dấu tiếng Việt trên thẻ.
    /// </summary>
    public string Model { get; init; } = "claude-opus-5";

    /// <summary>low | medium | high | xhigh | max. Trích xuất là bài toán tri giác, không cần suy luận sâu.</summary>
    public string Effort { get; init; } = "medium";

    /// <summary>Đủ cho ~15 field text; JSON schema đã chặn model viết dài.</summary>
    public int MaxOutputTokens { get; init; } = 2000;

    /// <summary>Vision chậm hơn FPT.AI (~3–6s) nên timeout phải rộng hơn.</summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// Khi Claude không gọi được (mất mạng, proxy chết, thiếu key) thì thử lại bằng FPT.AI.
    /// KHÔNG fallback khi Claude trả lời thành công nhưng ảnh sai/không đọc được — xem
    /// <c>FallbackCitizenIdOcrService</c>.
    /// </summary>
    public bool FallbackToFptAi { get; init; } = true;
}
