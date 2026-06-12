namespace MPS.Application.DTOs;

public sealed record ContractTemplateDto(
    int Id,
    string Name,
    string ContractCategory,
    string ContractSubtype,
    int Version,
    bool IsActive,
    DateTime CreatedAt,
    int? TemplateFileId,
    string? FileName);
