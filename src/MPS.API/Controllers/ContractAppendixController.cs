using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MPS.API.RateLimiting;
using MPS.API.Authorization;
using MPS.API.Requests.ContractAppendix;
using MPS.Application.Features.ContractAppendix.Commands;
using MPS.Application.Features.ContractAppendix.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Authorize]
public class ContractAppendixController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractAppendixController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// POST /api/contracts/{contractId}/appendices — tạo phụ lục ở trạng thái Draft.
    /// Handler set NoticeSentAt=NOW() và enforce Điều 33 (3 working-day notice).
    /// </summary>
    [HttpPost("api/contracts/{contractId:int}/appendices")]
    [HasPermission(Permissions.ContractAppendixCreateAll)]
    public async Task<IActionResult> Create(
        int contractId,
        [FromBody] CreateContractAppendixRequest request,
        CancellationToken cancellationToken)
    {
        var cmd = new CreateContractAppendixCommand(
            ContractId: contractId,
            Title: request.Title,
            ChangedClausesJson: request.ChangedClausesJson,
            AppendixText: request.AppendixText,
            EffectiveFrom: request.EffectiveFrom);

        var result = await _mediator.Send(cmd, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>GET /api/contracts/{contractId}/appendices — list theo contract.</summary>
    [HttpGet("api/contracts/{contractId:int}/appendices")]
    [HasAnyPermission(Permissions.ContractAppendixReadAll, Permissions.ContractAppendixReadSelf)]
    public async Task<IActionResult> GetByContract(int contractId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContractAppendicesQuery(contractId), cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/contract-appendices/{id} — chi tiết 1 phụ lục.</summary>
    [HttpGet("api/contract-appendices/{id:int}")]
    [HasAnyPermission(Permissions.ContractAppendixReadAll, Permissions.ContractAppendixReadSelf)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContractAppendixByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// GET /api/contract-appendices/{id}/files — list PDF của phụ lục (TargetType=Appendix),
    /// kèm presigned download URL TTL 15 phút. Self-scope check ở handler qua Contract.EmployeeId.
    /// </summary>
    [HttpGet("api/contract-appendices/{id:int}/files")]
    [HasAnyPermission(Permissions.ContractFileReadAll, Permissions.ContractFileReadSelf)]
    public async Task<IActionResult> GetFiles(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAppendixFilesQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/contract-appendices/{id}/activate — upload PDF + apply ChangedClausesJson lên HĐ.
    /// Re-uses the "contract-upload" rate-limit policy (5/min/user).
    /// </summary>
    [HttpPost("api/contract-appendices/{id:int}/activate")]
    [HasPermission(Permissions.ContractAppendixActivateAll)]
    [EnableRateLimiting(RateLimitPolicies.ContractUpload)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Activate(
        int id,
        [FromForm] ActivateContractAppendixRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length <= 0)
            return BadRequest(new { errors = new[] { "File is required." } });

        await using var stream = request.File.OpenReadStream();
        var seekableStream = await EnsureSeekableAsync(stream, request.File.Length, cancellationToken);

        var contractFileId = await _mediator.Send(new ActivateContractAppendixCommand(
            AppendixId: id,
            PdfStream: seekableStream,
            FileSizeBytes: request.File.Length,
            FileName: request.File.FileName,
            ContentType: request.File.ContentType ?? string.Empty,
            SignedAt: request.SignedAt,
            SignedBySource: request.SignedBySource), cancellationToken);

        return Ok(new { contractFileId });
    }

    /// <summary>Same rationale as ContractController.EnsureSeekableAsync.</summary>
    private static async Task<Stream> EnsureSeekableAsync(Stream source, long expectedLength, CancellationToken ct)
    {
        if (source.CanSeek) return source;
        var ms = new MemoryStream(capacity: (int)Math.Min(expectedLength, int.MaxValue));
        await source.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}
