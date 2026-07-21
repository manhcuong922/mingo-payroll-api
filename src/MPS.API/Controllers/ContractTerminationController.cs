using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MPS.API.Authorization;
using MPS.API.Requests.ContractTermination;
using MPS.Application.Features.ContractTermination.Commands;
using MPS.Application.Features.ContractTermination.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/contracts/{contractId:int}/termination")]
[Authorize]
public class ContractTerminationController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractTerminationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// POST /api/contracts/{contractId}/termination/notice — Upload PDF văn bản thông báo
    /// (Điều 45). Re-uses "contract-upload" rate-limit (5/min/user).
    /// </summary>
    [HttpPost("notice")]
    [HasPermission(Permissions.ContractFileUploadAll)]
    [EnableRateLimiting("contract-upload")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadNotice(
        int contractId,
        [FromForm] UploadTerminationNoticeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length <= 0)
            return BadRequest(new { errors = new[] { "File is required." } });

        await using var stream = request.File.OpenReadStream();
        var seekableStream = await EnsureSeekableAsync(stream, request.File.Length, cancellationToken);

        var fileId = await _mediator.Send(new UploadTerminationNoticeCommand(
            ContractId: contractId,
            PdfStream: seekableStream,
            FileSizeBytes: request.File.Length,
            FileName: request.File.FileName,
            ContentType: request.File.ContentType ?? string.Empty,
            SignedBySource: request.SignedBySource), cancellationToken);

        return Ok(new { contractFileId = fileId });
    }

    /// <summary>POST /api/contracts/{contractId}/termination — Tạo bản ghi chấm dứt + đóng HĐ.</summary>
    [HttpPost]
    [HasPermission(Permissions.ContractTerminationCreateAll)]
    public async Task<IActionResult> Create(
        int contractId,
        [FromBody] CreateContractTerminationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateContractTerminationCommand(
            ContractId: contractId,
            ReasonRefId: request.ReasonRefId,
            TerminationParty: request.TerminationParty,
            TerminationDate: request.TerminationDate,
            NoticeSentAt: request.NoticeSentAt,
            NoticeDaysAhead: request.NoticeDaysAhead,
            NoNoticeRequired: request.NoNoticeRequired,
            WrittenNoticeFileId: request.WrittenNoticeFileId,
            LegalBasis: request.LegalBasis,
            Notes: request.Notes), cancellationToken);

        // openTasks = cảnh báo bàn giao (#28); rỗng nghĩa là NV không còn công đoạn dở.
        return Ok(new { id = result.Id, openTasks = result.OpenTasks });
    }

    /// <summary>GET /api/contracts/{contractId}/termination — 1-1.</summary>
    [HttpGet]
    [HasAnyPermission(Permissions.ContractTerminationReadAll, Permissions.ContractTerminationReadSelf)]
    public async Task<IActionResult> Get(int contractId, CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new GetContractTerminationQuery(contractId), cancellationToken);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    private static async Task<Stream> EnsureSeekableAsync(Stream source, long expectedLength, CancellationToken ct)
    {
        if (source.CanSeek) return source;
        var ms = new MemoryStream(capacity: (int)Math.Min(expectedLength, int.MaxValue));
        await source.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}
