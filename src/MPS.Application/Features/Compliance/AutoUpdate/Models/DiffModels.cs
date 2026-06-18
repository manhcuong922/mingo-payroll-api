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
}
