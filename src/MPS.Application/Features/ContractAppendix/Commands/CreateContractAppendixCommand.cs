using MediatR;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.ContractAppendix.Commands;

/// <summary>
/// Phase 7 — Tạo phụ lục HĐ ở trạng thái Draft, set NoticeSentAt = NOW().
/// <c>AppendixNo</c> và <c>AppendixType</c> do BE tự sinh dựa trên ContractNo và nội dung
/// ChangedClausesJson, FE không cần truyền lên (Điều 22 + Điều 33 chạy trong validator/handler).
/// </summary>
public sealed record CreateContractAppendixCommand(
    int ContractId,
    string Title,
    string ChangedClausesJson,
    string AppendixText,
    DateTime EffectiveFrom) : IRequest<CreateContractAppendixResult>, IContractScopedCommand;

public sealed record CreateContractAppendixResult(
    int Id,
    string AppendixNo,
    string AppendixType);
