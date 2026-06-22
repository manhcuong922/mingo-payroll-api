namespace MPS.Application.DTOs.Compliance;

/// <summary>DTO hiển thị proposal cập nhật pháp luật (cho UI review).</summary>
public sealed record ComplianceUpdateProposalDto(
    int Id,
    string SourceKey,
    string DocumentUrl,
    string? DocumentNo,
    string TargetEntityType,
    DateOnly? ProposedValidFrom,
    string ExtractedJson,
    string DiffJson,
    string ValidationReport,
    string Status,
    string? ReviewNote,
    DateTime CreatedAt,
    DateTime? ReviewedAt);
