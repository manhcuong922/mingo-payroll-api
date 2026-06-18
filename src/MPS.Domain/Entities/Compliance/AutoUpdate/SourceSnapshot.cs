namespace MPS.Domain.Entities.Compliance.AutoUpdate;

/// <summary>
/// Cấp 1 — fingerprint danh sách "văn bản mới" của mỗi nguồn.
/// Chỉ hash phần ổn định (tiêu đề + số hiệu) để phát hiện danh sách có thay đổi hay không.
/// </summary>
public sealed class SourceSnapshot
{
    public int Id { get; set; }

    /// <summary>"vbpl" | "congbao" — UNIQUE.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>SHA256 hex (64 ký tự) của phần ổn định trong danh sách.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
