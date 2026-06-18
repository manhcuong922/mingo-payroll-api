namespace MPS.Application.Options;

/// <summary>
/// Cấu hình module tự động quét & đề xuất cập nhật pháp luật. Bind từ section "ComplianceAutoUpdate".
/// Tách riêng khỏi <see cref="ComplianceOptions"/> (section "Compliance") để không đụng nhau.
/// </summary>
public sealed class ComplianceAutoUpdateOptions
{
    public const string SectionName = "ComplianceAutoUpdate";

    /// <summary>Số giờ giữa mỗi lần chạy khi đã chuyển sang background (chưa dùng ở chế độ endpoint).</summary>
    public int CrawlIntervalHours { get; set; } = 6;

    /// <summary>true = chạy toàn luồng nhưng KHÔNG ghi DB (chỉ trả tóm tắt) — hữu ích test với nguồn thật.</summary>
    public bool DryRun { get; set; }

    public int RequestDelayMs { get; set; } = 1500;
    public string UserAgent { get; set; } = "Mozilla/5.0 (compatible; MingoComplianceBot/1.0)";

    public Dictionary<string, SourceOptions> Sources { get; set; } = new();
    public LlmOptions Llm { get; set; } = new();
}

public sealed class SourceOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = string.Empty;
}

public sealed class LlmOptions
{
    public string Provider { get; set; } = "anthropic";

    /// <summary>Tên biến môi trường chứa API key — KHÔNG hardcode key.</summary>
    public string ApiKeyEnvVar { get; set; } = "OPERIX_LLM_API_KEY";
    public string Model { get; set; } = "claude-opus-4-8";
    public int MaxInputChars { get; set; } = 12000;

    /// <summary>
    /// Endpoint Messages API. Để trống = gọi thẳng Anthropic.
    /// Trỏ sang CLIProxyAPI cục bộ (vd "http://localhost:8317") để dùng OAuth, không tính tiền token.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
