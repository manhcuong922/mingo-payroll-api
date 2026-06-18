namespace MPS.Application.Features.Compliance.AutoUpdate.Models;

/// <summary>Tham chiếu một văn bản trong danh sách "văn bản mới" của nguồn.</summary>
public sealed record LegalDocumentRef
{
    public required string SourceKey { get; init; }
    public required string Url { get; init; }
    public string? Title { get; init; }
    public string? DocumentNo { get; init; }
}
