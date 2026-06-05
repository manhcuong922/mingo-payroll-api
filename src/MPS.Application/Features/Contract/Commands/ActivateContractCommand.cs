using MediatR;

namespace MPS.Application.Features.Contract.Commands;

/// <summary>
/// Phase 6 — Upload PDF đã ký + chuyển DRAFT→ACTIVE + đóng băng snapshot lương.
/// Returns the new ContractFile.Id.
/// </summary>
public sealed record ActivateContractCommand(
    int ContractId,
    Stream PdfStream,
    long FileSizeBytes,
    string FileName,
    string ContentType,
    DateTime SignedAt,
    string SignedPlace,
    string? SignedBySource) : IRequest<int>;
