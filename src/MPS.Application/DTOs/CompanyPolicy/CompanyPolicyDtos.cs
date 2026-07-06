namespace MPS.Application.DTOs.CompanyPolicy;

public sealed record CompanyPolicySummaryDto(
    int Id,
    string Title,
    string? Description,
    bool IsActive,
    int VersionCount,
    CompanyPolicyFileDto? CurrentFile,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CompanyPolicyDetailDto(
    int Id,
    string Title,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    int CreatedByUserId,
    DateTime? UpdatedAt,
    int? UpdatedByUserId,
    IReadOnlyList<CompanyPolicyFileDto> Files);

public sealed record CompanyPolicyFileDto(
    int Id,
    int Version,
    string FileName,
    string MimeType,
    long FileSizeBytes,
    string? Notes,
    bool IsCurrent,
    DateTime UploadedAt,
    int UploadedByUserId,
    string? UploadedByUserFullName);
