namespace MPS.Domain.Entities.Compliance.AutoUpdate;

public static class ProposalStatuses
{
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

/// <summary>
/// Các loại entity compliance mà một proposal có thể nhắm tới.
/// Tên = tên class entity thật trong <c>MPS.Domain.Entities.Compliance</c> để khớp diff/Set&lt;T&gt;().
/// OtRate đã bị loại khỏi hệ thống nên KHÔNG có ở đây.
/// </summary>
public static class TargetEntityTypes
{
    public const string SocialInsuranceRateVersion = "SocialInsuranceRateVersion";
    public const string MinimumWageVersion = "MinimumWageVersion";
    public const string PitRegulationVersion = "PitRegulationVersion";
    public const string LaborLawConstantsVersion = "LaborLawConstantsVersion";
    public const string AllowanceExemptThresholdVersion = "AllowanceExemptThresholdVersion";

    public static readonly string[] All =
    {
        SocialInsuranceRateVersion, MinimumWageVersion, PitRegulationVersion,
        LaborLawConstantsVersion, AllowanceExemptThresholdVersion,
    };
}

/// <summary>
/// Đề xuất cập nhật pháp luật chờ duyệt. Module tự động KHÔNG bao giờ tự ghi đè data đang active —
/// chỉ tạo proposal trạng thái <see cref="ProposalStatuses.PendingReview"/> để admin duyệt sau.
/// </summary>
public sealed class ComplianceUpdateProposal
{
    public int Id { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string? DocumentNo { get; set; }

    /// <summary>Xem <see cref="TargetEntityTypes"/>.</summary>
    public string TargetEntityType { get; set; } = string.Empty;
    public DateOnly? ProposedValidFrom { get; set; }

    /// <summary>JSON gốc AI bóc ra (kèm evidence từng field).</summary>
    public string ExtractedJson { get; set; } = string.Empty;

    /// <summary>Diff giữa data active và data đề xuất.</summary>
    public string DiffJson { get; set; } = string.Empty;

    /// <summary>Kết quả 3 lớp validation.</summary>
    public string ValidationReport { get; set; } = string.Empty;

    /// <summary>Xem <see cref="ProposalStatuses"/>. Mặc định PendingReview.</summary>
    public string Status { get; set; } = ProposalStatuses.PendingReview;
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
