using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Compliance.AutoUpdate.Ingestion;

/// <summary>Nguồn nạp tay (Kênh A) — phân biệt với crawl trên bảng proposal.</summary>
public static class ManualIntakeSources
{
    public const string Upload = "manual-upload";
    public const string Url = "manual-url";
}

/// <summary>
/// Kênh A — nạp một văn bản (file đã đọc thành bytes, hoặc URL) → lõi xử lý → (tạo) proposal.
/// Đúng một trong hai: <see cref="FileBytes"/> hoặc <see cref="Url"/>.
/// </summary>
public sealed record IngestDocumentCommand(
    byte[]? FileBytes,
    string? FileName,
    string? Url,
    string? DocumentNo) : IRequest<IngestDocumentResult>;

/// <summary>Kết quả nạp một văn bản, đủ để UI hiển thị toast + điều hướng tới proposal.</summary>
public sealed record IngestDocumentResult(
    string Outcome,        // tên IngestionKind: Proposal | NoChange | NoScopedContent | Failed
    int? ProposalId,
    string Message,
    int WarningCount,
    int ErrorCount);

public sealed class IngestDocumentCommandHandler : IRequestHandler<IngestDocumentCommand, IngestDocumentResult>
{
    private readonly IDocumentTextFetcher _fetcher;
    private readonly IComplianceDocumentProcessor _processor;
    private readonly ILogger<IngestDocumentCommandHandler> _logger;

    public IngestDocumentCommandHandler(
        IDocumentTextFetcher fetcher,
        IComplianceDocumentProcessor processor,
        ILogger<IngestDocumentCommandHandler> logger)
    {
        _fetcher = fetcher;
        _processor = processor;
        _logger = logger;
    }

    public async Task<IngestDocumentResult> Handle(IngestDocumentCommand request, CancellationToken ct)
    {
        var hasFile = request.FileBytes is { Length: > 0 };
        var hasUrl = !string.IsNullOrWhiteSpace(request.Url);
        if (hasFile == hasUrl)
            throw new DomainValidationException("Cần cung cấp đúng một trong hai: file hoặc URL.");

        string sourceKey, fullText;
        string? url = null;

        if (hasFile)
        {
            sourceKey = ManualIntakeSources.Upload;
            fullText = await _fetcher.FromBytesAsync(request.FileBytes!, request.FileName ?? string.Empty, ct);
        }
        else
        {
            sourceKey = ManualIntakeSources.Url;
            url = request.Url!.Trim();
            fullText = await _fetcher.FromUrlAsync(url, ct);
        }

        if (string.IsNullOrWhiteSpace(fullText))
            return new IngestDocumentResult(
                IngestionKind.Failed.ToString(), null,
                "Không đọc được nội dung text — nghi PDF scan/ảnh hoặc trang nạp bằng JavaScript.", 0, 0);

        var outcome = await _processor.ProcessAsync(
            new RawDocument(sourceKey, url, request.DocumentNo?.Trim(), fullText), ct);

        var warn = outcome.Report?.WarningCount ?? 0;
        var err = outcome.Report?.ErrorCount ?? 0;
        var message = outcome.Kind switch
        {
            IngestionKind.Proposal => outcome.ProposalId is { } id
                ? $"Đã tạo đề xuất #{id} — chờ duyệt."
                : "Phát hiện thay đổi (chế độ DryRun, không lưu).",
            IngestionKind.NoChange => "Số liệu trùng với dữ liệu đang áp dụng — không tạo đề xuất.",
            IngestionKind.NoScopedContent =>
                "Không tìm thấy điều khoản thuộc phạm vi (BHXH/thuế TNCN/lương tối thiểu/luật lao động).",
            _ => outcome.Reason ?? "Xử lý thất bại.",
        };

        _logger.LogInformation(
            "[Intake] nguồn={Source} → {Kind}, proposal={Id}, warn={Warn}, err={Err}.",
            sourceKey, outcome.Kind, outcome.ProposalId, warn, err);

        return new IngestDocumentResult(outcome.Kind.ToString(), outcome.ProposalId, message, warn, err);
    }
}
