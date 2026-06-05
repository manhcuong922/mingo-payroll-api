using MediatR;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.ContractTermination.Commands;

/// <summary>
/// Phase 8 — Upload PDF văn bản thông báo chấm dứt HĐLĐ (Điều 45).
/// Tách khỏi <see cref="CreateContractTerminationCommand"/> để HR có thể chuẩn bị file trước,
/// vài ngày sau (sau khi đủ thời gian báo trước) mới ký quyết định termination.
/// Returns ContractFile.Id.
/// </summary>
public sealed record UploadTerminationNoticeCommand(
    int ContractId,
    Stream PdfStream,
    long FileSizeBytes,
    string FileName,
    string ContentType,
    string? SignedBySource) : IRequest<int>, IContractScopedCommand;
