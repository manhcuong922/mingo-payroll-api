namespace MPS.Infrastructure.Email;

/// <summary>
/// Cấu hình worker gửi email từ outbox (section "EmailDispatch"). Có default hợp lý nên
/// không bắt buộc khai báo trong appsettings.
/// </summary>
public sealed class EmailDispatchOptions
{
    public const string SectionName = "EmailDispatch";

    /// <summary>Chu kỳ poll bảng outbox (giây).</summary>
    public int PollingIntervalSeconds { get; set; } = 10;

    /// <summary>Số message xử lý mỗi lượt.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Số lần thử tối đa trước khi đánh dấu Failed (≈ DLQ).</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Trễ nền cho exponential backoff: delay = BaseBackoffSeconds * 2^(attempts-1).</summary>
    public int BaseBackoffSeconds { get; set; } = 30;

    /// <summary>Số ngày giữ lại các dòng đã gửi (Sent) trước khi job dọn xóa đi.</summary>
    public int SentRetentionDays { get; set; } = 7;
}
