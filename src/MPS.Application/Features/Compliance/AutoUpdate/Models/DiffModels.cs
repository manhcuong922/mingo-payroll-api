namespace MPS.Application.Features.Compliance.AutoUpdate.Models;

public enum ChangeType { Unchanged, Changed, Added, Removed }

public sealed record DiffEntry(
    string Field,
    string? OldValue,
    string? NewValue,
    ChangeType ChangeType);

/// <summary>Kết quả so sánh data active vs data đề xuất.</summary>
public sealed class DiffResult
{
    public List<DiffEntry> Entries { get; } = new();

    /// <summary>true nếu có ít nhất 1 field Changed/Added/Removed.</summary>
    public bool HasSubstantiveChange =>
        Entries.Any(e => e.ChangeType != ChangeType.Unchanged);

    public int ChangedCount => Entries.Count(e => e.ChangeType == ChangeType.Changed);
    public int AddedCount => Entries.Count(e => e.ChangeType == ChangeType.Added);
    public int RemovedCount => Entries.Count(e => e.ChangeType == ChangeType.Removed);
    public int UnchangedCount => Entries.Count(e => e.ChangeType == ChangeType.Unchanged);

    /// <summary>Số field đang có trong data active (Unchanged + Changed + Removed).</summary>
    public int ActiveFieldCount => UnchangedCount + ChangedCount + RemovedCount;

    /// <summary>Số field active mà lần bóc này lấy được giá trị (Unchanged + Changed).</summary>
    public int CoveredFieldCount => UnchangedCount + ChangedCount;

    /// <summary>
    /// Tỷ lệ field active mà AI bóc lại được. Thấp = AI bóc hụt chứ không phải văn bản bãi bỏ:
    /// mỗi field hụt biến thành một entry <see cref="ChangeType.Removed"/>, và nếu tin đó là
    /// "thay đổi thực chất" thì proposal được tạo ra sẽ XOÁ đúng dữ liệu đang chuẩn.
    /// Data active rỗng → trả 1 (không có gì để hụt).
    /// </summary>
    public double ActiveFieldCoverage =>
        ActiveFieldCount == 0 ? 1d : (double)CoveredFieldCount / ActiveFieldCount;
}
