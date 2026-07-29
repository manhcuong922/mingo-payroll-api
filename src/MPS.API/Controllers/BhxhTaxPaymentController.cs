using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.BhxhTaxPayment;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Payroll;

namespace MPS.API.Controllers;

/// <summary>
/// Module BHXH & Tax Payment Tracking (v3 line-level): quản lý vòng đời nghĩa vụ nộp BHXH/TNCN.
/// Payment ghi nhận theo từng NV (line); evidence upload nhiều ảnh ở cha; user bấm nút "Hoàn thành"
/// để chốt (khoá mọi mutation).
/// </summary>
[ApiController]
[Route("api/bhxh-tax-payment")]
public class BhxhTaxPaymentController : ControllerBase
{
    private readonly IMediator _mediator;
    public BhxhTaxPaymentController(IMediator mediator) => _mediator = mediator;

    // ===== Obligation-level =====

    [HttpGet("obligations")]
    [HasPermission(Permissions.BhxhTaxPaymentReadAll)]
    public async Task<IActionResult> GetObligations(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] PaymentObligationType? type,
        [FromQuery] PaymentObligationStatus? status,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetPaymentObligationsQuery(year, month, type, status), ct));

    [HttpGet("obligations/{id:int}")]
    [HasPermission(Permissions.BhxhTaxPaymentReadAll)]
    public async Task<IActionResult> GetObligationById(int id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetPaymentObligationByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Bấm nút "Hoàn thành" — chốt nghĩa vụ (mọi line ∈ {Paid, Excluded} + ≥1 evidence).</summary>
    [HttpPost("obligations/{id:int}/complete")]
    [HasPermission(Permissions.BhxhTaxPaymentConfirmAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CompleteObligation(int id, CancellationToken ct)
    {
        await _mediator.Send(new CompleteObligationCommand(id), ct);
        return NoContent();
    }

    // ===== Line-level =====

    [HttpGet("obligations/{id:int}/lines")]
    [HasPermission(Permissions.BhxhTaxPaymentReadAll)]
    public async Task<IActionResult> GetLines(
        int id,
        [FromQuery] PaymentLineStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetPaymentObligationLinesQuery(id, status, search, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("obligations/{id:int}/lines/export")]
    [HasPermission(Permissions.BhxhTaxPaymentReadAll)]
    public async Task<IActionResult> ExportLines(int id, CancellationToken ct)
    {
        var file = await _mediator.Send(new ExportPaymentObligationLinesQuery(id), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("lines/{lineId:int}/record")]
    [HasPermission(Permissions.BhxhTaxPaymentRecordAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordLine(
        int lineId, [FromBody] RecordLinePaymentRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RecordLinePaymentCommand(
            LineId: lineId,
            PaidDate: request.PaidDate,
            PaidAmount: request.PaidAmount,
            PaymentMethod: request.PaymentMethod,
            TransactionRef: request.TransactionRef,
            PaymentNotes: request.PaymentNotes,
            RowVersion: request.RowVersion), ct);
        return NoContent();
    }

    [HttpPost("lines/{lineId:int}/exclude")]
    [HasPermission(Permissions.BhxhTaxPaymentRecordAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ExcludeLine(
        int lineId, [FromBody] ExcludeLineRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ExcludeLineCommand(lineId, request.ExcludedReason, request.RowVersion), ct);
        return NoContent();
    }

    [HttpPost("lines/{lineId:int}/revert")]
    [HasPermission(Permissions.BhxhTaxPaymentRecordAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevertLine(
        int lineId, [FromBody] RevertLineRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RevertLineCommand(lineId, request.RowVersion), ct);
        return NoContent();
    }

    // ===== Evidence =====

    [HttpGet("obligations/{id:int}/evidence")]
    [HasPermission(Permissions.BhxhTaxPaymentReadAll)]
    public async Task<IActionResult> ListEvidence(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new ListEvidenceQuery(id), ct));

    [HttpPost("obligations/{id:int}/evidence")]
    [HasPermission(Permissions.BhxhTaxPaymentRecordAll)]
    public async Task<IActionResult> UploadEvidence(
        int id, [FromForm] UploadEvidenceRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length <= 0)
            return BadRequest(new { errors = new[] { "file là bắt buộc." } });

        await using var stream = request.File.OpenReadStream();
        var evidenceId = await _mediator.Send(new UploadObligationEvidenceCommand(
            ObligationId: id,
            Content: stream,
            ContentType: request.File.ContentType ?? string.Empty,
            SizeBytes: request.File.Length,
            OriginalFileName: request.File.FileName ?? string.Empty,
            Description: request.Description), ct);
        return Ok(new { evidenceId });
    }

    [HttpGet("evidence/{evidenceId:int}/download-url")]
    [HasPermission(Permissions.BhxhTaxPaymentReadAll)]
    public async Task<IActionResult> GetEvidenceDownloadUrl(int evidenceId, CancellationToken ct)
    {
        var url = await _mediator.Send(new GetEvidenceDownloadUrlQuery(evidenceId), ct);
        return url is null ? NotFound() : Ok(new { downloadUrl = url });
    }

    [HttpDelete("evidence/{evidenceId:int}")]
    [HasPermission(Permissions.BhxhTaxPaymentRecordAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteEvidence(int evidenceId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteObligationEvidenceCommand(evidenceId), ct);
        return NoContent();
    }
}
