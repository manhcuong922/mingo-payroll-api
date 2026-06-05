using MediatR;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractAppendix.Commands;

/// <summary>
/// Phase 7 — Tạo phụ lục HĐ ở trạng thái Draft, set NoticeSentAt = NOW().
/// Validation Điều 22 + Điều 33 chạy trong validator/handler.
/// </summary>
public sealed record CreateContractAppendixCommand(
    int ContractId,
    string AppendixNo,
    ContractEnums.AppendixType AppendixType,
    string Title,
    string ChangedClausesJson,
    string AppendixText,
    DateTime EffectiveFrom) : IRequest<int>;
