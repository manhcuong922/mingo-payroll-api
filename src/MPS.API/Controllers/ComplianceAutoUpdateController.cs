using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
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
    private readonly ComplianceAutoUpdateJob _job;
    private readonly IMediator _mediator;

    public ComplianceAutoUpdateController(ComplianceAutoUpdateJob job, IMediator mediator)
    {
        _job = job;
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
}
