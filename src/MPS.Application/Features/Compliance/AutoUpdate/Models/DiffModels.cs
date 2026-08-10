namespace MPS.Application.Features.Compliance.AutoUpdate.Models;

/// <summary>
/// Loại thay đổi của một field khi so data đề xuất với data đang áp dụng.
///
/// <see cref="Removed"/> GIỮ LẠI cho DiffJson cũ đã lưu trong DB (giá trị 3) — code hiện tại
/// không sinh ra nó nữa. Field active mà AI không bóc lại được giờ là <see cref="NotExtracted"/>:
/// applier luôn lấy giá trị cũ làm nền (xem <c>ComplianceProposalApplier</c>) nên đó KHÔNG phải
/// đề xuất xoá, gọi là "Removed" chỉ khiến người duyệt hiểu sai.
/// </summary>
public enum ChangeType { Unchanged, Changed, Added, Removed, NotExtracted }

public sealed record DiffEntry(
    string Field,
    string? OldValue,
    string? NewValue,
    ChangeType ChangeType);

/// <summary>Kết quả so sánh data active vs data đề xuất.</summary>
public sealed class DiffResult
{
    public List<DiffEntry> Entries { get; } = new();

    /// <summary>
    /// ValidFrom của version đang active (null nếu chưa có version nào). Dùng để chặn văn bản
    /// CŨ HƠN dữ liệu đang áp dụng — xem chốt recency trong <c>ComplianceDocumentProcessor</c>.
    /// </summary>
    public DateOnly? ActiveValidFrom { get; set; }

    /// <summary>
    /// true nếu có ít nhất 1 field Changed/Added — tức là có thứ applier thực sự ghi được.
    /// NotExtracted/Removed KHÔNG tính: applier giữ nguyên giá trị active cho những field đó,
    /// nên tạo proposal chỉ vì chúng là tạo proposal rỗng.
    /// </summary>
    public bool HasSubstantiveChange =>
        Entries.Any(e => e.ChangeType is ChangeType.Changed or ChangeType.Added);

    public int ChangedCount => Entries.Count(e => e.ChangeType == ChangeType.Changed);
    public int AddedCount => Entries.Count(e => e.ChangeType == ChangeType.Added);
    public int RemovedCount => Entries.Count(e => e.ChangeType == ChangeType.Removed);
    public int NotExtractedCount => Entries.Count(e => e.ChangeType == ChangeType.NotExtracted);
    public int UnchangedCount => Entries.Count(e => e.ChangeType == ChangeType.Unchanged);

    /// <summary>Số field đang có trong data active (mọi loại trừ Added).</summary>
    public int ActiveFieldCount => UnchangedCount + ChangedCount + RemovedCount + NotExtractedCount;

    /// <summary>Số field active mà lần bóc này lấy được giá trị (Unchanged + Changed).</summary>
    public int CoveredFieldCount => UnchangedCount + ChangedCount;

    /// <summary>
    /// Tỷ lệ field active mà AI bóc lại được. Thấp = AI bóc hụt (thường do văn bản bị cắt hoặc
    /// prompt thiếu tên field) chứ không phải văn bản bãi bỏ — proposal sinh ra khi đó vô nghĩa.
    /// Data active rỗng → trả 1 (không có gì để hụt).
    /// </summary>
    public double ActiveFieldCoverage =>
        ActiveFieldCount == 0 ? 1d : (double)CoveredFieldCount / ActiveFieldCount;
}
