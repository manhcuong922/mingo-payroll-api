namespace MPS.Application.DTOs;

public sealed record ContractAppendixDto(
    int Id,
    int ContractId,
    string AppendixNo,
    string AppendixType,
    string Title,
    string ChangedClausesJson,
    string AppendixText,
    DateTime? NoticeSentAt,
    DateTime EffectiveFrom,
    DateTime? SignedAt,
    string Status,
    DateTime CreatedAt,
    int CreatedByUserId);
