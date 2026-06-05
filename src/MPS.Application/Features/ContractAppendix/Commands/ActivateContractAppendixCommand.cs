using MediatR;

namespace MPS.Application.Features.ContractAppendix.Commands;

/// <summary>
/// Phase 7 — Upload PDF phụ lục đã ký + chuyển DRAFT→ACTIVE + apply ChangedClausesJson
/// lên contract gốc (transaction). Returns the new ContractFile.Id.
/// </summary>
public sealed record ActivateContractAppendixCommand(
    int AppendixId,
    Stream PdfStream,
    long FileSizeBytes,
    string FileName,
    string ContentType,
    DateTime SignedAt,
    string? SignedBySource) : IRequest<int>;
