namespace MPS.Application.DTOs;

public sealed record ContractTemplateDto(
    int Id,
    string Name,
    string ContractCategory,
    string ContractSubtype,
    string TemplateBody,
    int Version,
    bool IsActive,
    DateTime CreatedAt);
