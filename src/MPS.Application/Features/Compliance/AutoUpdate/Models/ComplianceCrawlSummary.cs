namespace MPS.Application.Features.Compliance.AutoUpdate.Models;

/// <summary>Tóm tắt một lần chạy pipeline auto-update (trả về cho endpoint kích hoạt).</summary>
public sealed record ComplianceCrawlSummary(
    Guid RunId,
    int SourcesChecked,
    int NewDocuments,
    int ProposalsCreated,
    int Failures,
    bool DryRun);
