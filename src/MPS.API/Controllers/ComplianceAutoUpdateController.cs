using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Compliance;
using MPS.Application.Features.Compliance.AutoUpdate.Approval;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Application.Features.Compliance.AutoUpdate.Queries;
using MPS.Domain.Constants;
using MPS.Infrastructure.BackgroundJobs;

namespace MPS.API.Controllers;

/// <summary>
/// Module tự động quét &amp; đề xuất cập nhật pháp luật.
/// Hiện chạy thủ công qua endpoint để test; sau khi đảm bảo độ chính xác sẽ chuyển sang background.
/// </summary>
[ApiController]
[Route("api/compliance/auto-update")]
public class ComplianceAutoUpdateController : ControllerBase
{
    private const long MaxUploadBytes = 15_728_640; // 15MB
    private static readonly IReadOnlySet<string> AllowedUploadExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".html", ".htm", ".txt" };

    private readonly ComplianceAutoUpdateJob _job;
    private readonly ComplianceWatcherJob _watcherJob;
    private readonly IMediator _mediator;

    public ComplianceAutoUpdateController(
        ComplianceAutoUpdateJob job, ComplianceWatcherJob watcherJob, IMediator mediator)
    {
        _job = job;
        _watcherJob = watcherJob;
        _mediator = mediator;
    }

    /// <summary>Kích hoạt một lần chạy pipeline (crawl → AI → validate → tạo proposal). Trả tóm tắt.</summary>
    [HttpPost("run")]
    [HasPermission(Permissions.ComplianceAutoUpdateRunAll)]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var summary = await _job.RunOnceAsync(cancellationToken);
        if (summary is null)
            return Conflict(new { message = "Đang có một lần chạy khác — vui lòng thử lại sau." });
        return Ok(summary);
    }

    /// <summary>Kênh A — nạp văn bản bằng file upload (PDF/HTML/txt) → tạo proposal nếu có thay đổi.</summary>
    [HttpPost("intake/upload")]
    [HasPermission(Permissions.ComplianceAutoUpdateRunAll)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> IntakeUpload(
        IFormFile file, [FromForm] string? documentNo, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Chưa chọn file." });
        if (file.Length > MaxUploadBytes)
            return BadRequest(new { message = "File quá lớn (tối đa 15MB)." });

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedUploadExtensions.Contains(ext))
            return BadRequest(new { message = "Chỉ chấp nhận .pdf, .html, .htm, .txt." });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);

        var result = await _mediator.Send(
            new IngestDocumentCommand(ms.ToArray(), file.FileName, null, documentNo), cancellationToken);
        return Ok(result);
    }

    /// <summary>Kênh A — nạp văn bản bằng URL (PDF/HTML) → tạo proposal nếu có thay đổi.</summary>
    [HttpPost("intake/url")]
    [HasPermission(Permissions.ComplianceAutoUpdateRunAll)]
    public async Task<IActionResult> IntakeUrl(
        [FromBody] IngestDocumentUrlRequest body, CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Url))
            return BadRequest(new { message = "Thiếu URL." });
        if (!Uri.TryCreate(body.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { message = "URL không hợp lệ (chỉ http/https)." });

        var result = await _mediator.Send(
            new IngestDocumentCommand(null, null, body.Url, body.DocumentNo), cancellationToken);
        return Ok(result);
    }

    /// <summary>Danh sách proposal gần nhất để review trên UI.</summary>
    [HttpGet("proposals")]
    [HasPermission(Permissions.ComplianceAutoUpdateReadAll)]
    public async Task<IActionResult> GetProposals(
        [FromQuery] string? status,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetComplianceProposalsQuery(status, take), cancellationToken);
        return Ok(result);
    }

    /// <summary>Kênh B — rà soát chủ động ngay (LLM + web search → Lead). Trả tóm tắt.</summary>
    [HttpPost("watch/run")]
    [HasPermission(Permissions.ComplianceAutoUpdateRunAll)]
    public async Task<IActionResult> RunWatch(CancellationToken cancellationToken)
    {
        var summary = await _watcherJob.RunOnceAsync(cancellationToken);
        if (summary is null)
            return Conflict(new { message = "Đang có một lần rà soát khác — vui lòng thử lại sau." });
        return Ok(summary);
    }

    /// <summary>Danh sách Lead (Kênh B) gần nhất để review trên UI.</summary>
    [HttpGet("leads")]
    [HasPermission(Permissions.ComplianceAutoUpdateReadAll)]
    public async Task<IActionResult> GetLeads(
        [FromQuery] string? status, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetComplianceLeadsQuery(status, take), cancellationToken);
        return Ok(result);
    }

    /// <summary>Văn bản đang chi phối từng loại entity compliance (móng cho Kênh B).</summary>
    [HttpGet("authoritative-sources")]
    [HasPermission(Permissions.ComplianceAutoUpdateReadAll)]
    public async Task<IActionResult> GetAuthoritativeSources(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAuthoritativeSourcesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Duyệt proposal → tạo version compliance mới (chờ hiệu lực theo ProposedValidFrom).</summary>
    [HttpPost("proposals/{id:int}/approve")]
    [HasPermission(Permissions.ComplianceAutoUpdateApproveAll)]
    public async Task<IActionResult> Approve(
        int id, [FromBody] ReviewProposalRequest? body, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApproveComplianceProposalCommand(id, body?.Note), cancellationToken);
        return Ok(result);
    }

    /// <summary>Từ chối proposal (không tạo version).</summary>
    [HttpPost("proposals/{id:int}/reject")]
    [HasPermission(Permissions.ComplianceAutoUpdateApproveAll)]
    public async Task<IActionResult> Reject(
        int id, [FromBody] ReviewProposalRequest? body, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RejectComplianceProposalCommand(id, body?.Note), cancellationToken);
        return Ok(result);
    }
}
