using MediatR;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

/// <summary>
/// Phase 6 — Upload a new signed PDF version after activation (HR phát hiện sai sau khi ký).
/// Old file gets IsCurrent=false (kept for history); new file gets Version=N+1 + IsCurrent=true.
/// Rejected if contract is archived. Returns new ContractFile.Id.
/// </summary>
public sealed record ReplaceContractFileCommand(
    int ContractId,
    Stream PdfStream,
    long FileSizeBytes,
    string FileName,
    string ContentType,
    string? SignedBySource) : IRequest<int>, IContractScopedCommand;
