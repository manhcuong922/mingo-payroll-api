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

    /// <summary>
    /// Tỷ lệ tối thiểu số field đang áp dụng mà AI phải bóc lại được thì proposal mới đáng tin
    /// (xem <c>DiffResult.ActiveFieldCoverage</c>). Dưới ngưỡng = AI bóc hụt, KHÔNG tạo proposal —
    /// vì proposal kiểu đó chỉ toàn field "removed" và duyệt nhầm là xoá sạch dữ liệu đang chuẩn.
    /// Đặt 0 để tắt chốt này.
    /// </summary>
    public double MinActiveFieldCoverage { get; set; } = 0.5;

    public Dictionary<string, SourceOptions> Sources { get; set; } = new();
    public LlmOptions Llm { get; set; } = new();
    public OcrOptions Ocr { get; set; } = new();
    public WatcherOptions Watcher { get; set; } = new();
}

/// <summary>Cấu hình Kênh B — watcher chủ động (LLM + web search → Lead).</summary>
public sealed class WatcherOptions
{
    /// <summary>Bật chạy nền định kỳ. Tắt thì chỉ chạy khi bấm "Dò ngay".</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Số ngày giữa mỗi lần chạy nền (mặc định hằng tuần).</summary>
    public int IntervalDays { get; set; } = 7;

    /// <summary>Model riêng cho watcher (rẻ hơn extract/OCR để giảm chi phí web_search). Rỗng = dùng Llm.Model.</summary>
    public string Model { get; set; } = "claude-sonnet-4-6";

    /// <summary>Giới hạn số lượt web_search mỗi entity (kiểm soát chi phí token).</summary>
    public int MaxSearchUses { get; set; } = 3;

    /// <summary>
    /// Phiên bản server tool web_search.
    ///
    /// <c>web_search_20250305</c> (mặc định) — search thuần, CHẠY ĐƯỢC qua CLIProxyAPI (OAuth Claude Pro),
    /// tức là nằm trọn trong session, không phát sinh phí server tool riêng.
    ///
    /// <c>web_search_20260209</c> — có dynamic filtering nên URL trả về sạch hơn, NHƯNG chạy ngầm bằng
    /// code execution; qua session Pro thì sandbox đó trả <c>too_many_requests</c>, search âm thầm
    /// KHÔNG chạy (web_search_requests = 0) và model bịa câu trả lời từ trí nhớ. Chỉ dùng khi gọi
    /// thẳng api.anthropic.com (Llm:BaseUrl rỗng).
    /// </summary>
    public string WebSearchToolType { get; set; } = "web_search_20250305";

    /// <summary>
    /// max_tokens cho câu trả lời của watcher. Phải rộng tay: web_search bản mới có dynamic filtering
    /// nên model hay viết dài; chạm trần → JSON cụt → parse fail → Lead NeedsHuman oan.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>
    /// Chỉ cho web_search tra trong các domain này (khớp cả subdomain). Mục đích: ép model trả URL
    /// TOÀN VĂN của trang pháp luật chính thống, thay vì link bài báo — bài báo không có cấu trúc
    /// "Điều N." nên <c>ContentScopeFilter</c> lọc ra 0 điều và pipeline dừng ở NoScopedContent.
    /// Rỗng = không giới hạn.
    ///
    /// CỐ Ý để rỗng, danh sách thật nằm ở appsettings.json: ConfigurationBinder của .NET NỐI THÊM
    /// phần tử từ config vào mảng mặc định chứ không thay thế — đặt giá trị ở cả hai nơi sẽ sinh
    /// domain trùng và Anthropic trả 400 "Domain list must not contain duplicates".
    /// </summary>
    public string[] AllowedDomains { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Cấu hình OCR PDF scan/ảnh bằng Claude vision (fallback Kênh A khi PdfPig không bóc được text).
/// </summary>
public sealed class OcrOptions
{
    /// <summary>Bật fallback OCR. Tự hiệu lực khi có API key; đặt false để tắt hẳn.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Text PdfPig bóc được dưới ngưỡng này (ký tự) → coi là PDF scan/ảnh, gọi OCR.</summary>
    public int MinPdfTextChars { get; set; } = 200;

    /// <summary>Số trang tối đa gửi cho vision (kiểm soát token). Vượt → bỏ qua, báo lỗi.</summary>
    public int MaxPdfPages { get; set; } = 30;

    /// <summary>
    /// Model riêng cho OCR (rẻ hơn extract để tiết kiệm token/quota — OCR chỉ chép text, không cần reasoning).
    /// Rỗng = dùng <see cref="LlmOptions.Model"/>.
    /// </summary>
    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>max_tokens cho bản phiên âm.</summary>
    public int MaxOutputTokens { get; set; } = 16000;
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
