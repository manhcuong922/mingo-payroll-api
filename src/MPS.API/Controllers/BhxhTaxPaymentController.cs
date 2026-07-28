using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.BhxhTaxPayment;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Payroll;

namespace MPS.API.Controllers;

/// <summary>
/// Module BHXH & Tax Payment Tracking: quản lý vòng đời nghĩa vụ nộp BHXH/TNCN sau khi
/// Payroll Period được khoá. Không tính toán lại — chỉ ghi nhận đã nộp + bằng chứng.
/// </summary>
[ApiController]
[Route("api/bhxh-tax-payment")]
public class BhxhTaxPaymentController : ControllerBase
{
    private readonly IMediator _mediator;
    public BhxhTaxPaymentController(IMediator mediator) => _mediator = mediator;

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

    /// <summary>Ghi nhận đã nộp (paidDate, paidAmount, method, transactionRef, notes).</summary>
    [HttpPost("obligations/{id:int}/record")]
    [HasPermission(Permissions.BhxhTaxPaymentRecordAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordPayment(
        int id, [FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RecordPaymentCommand(
            ObligationId: id,
            PaidDate: request.PaidDate,
            PaidAmount: request.PaidAmount,
            PaymentMethod: request.PaymentMethod,
            TransactionRef: request.TransactionRef,
            Notes: request.Notes), ct);
        return NoContent();
    }

    /// <summary>Xác nhận chấp nhận số chênh lệch (Discrepancy → Confirmed).</summary>
    [HttpPost("obligations/{id:int}/confirm-discrepancy")]
    [HasPermission(Permissions.BhxhTaxPaymentConfirmAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmDiscrepancy(
        int id, [FromBody] ConfirmDiscrepancyRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ConfirmPaymentDiscrepancyCommand(id, request.ConfirmationNote), ct);
        return NoContent();
    }

    /// <summary>Upload ảnh biên lai chuyển khoản (multipart, JPG/PNG/WEBP ≤5MB).</summary>
    [HttpPost("obligations/{id:int}/evidence/bank-receipt")]
    [HasPermission(Permissions.BhxhTaxPaymentRecordAll)]
    public Task<IActionResult> UploadBankReceipt(
        int id, [FromForm] UploadEvidenceRequest request, CancellationToken ct)
        => UploadEvidenceInternal(id, BhxhTaxEvidenceKind.BankReceipt, request, ct);

    /// <summary>Upload ảnh xác nhận từ cơ quan BHXH/Thuế (multipart, JPG/PNG/WEBP ≤5MB).</summary>
    [HttpPost("obligations/{id:int}/evidence/authority-confirmation")]
    [HasPermission(Permissions.BhxhTaxPaymentRecordAll)]
    public Task<IActionResult> UploadAuthorityConfirmation(
        int id, [FromForm] UploadEvidenceRequest request, CancellationToken ct)
        => UploadEvidenceInternal(id, BhxhTaxEvidenceKind.AuthorityConfirmation, request, ct);

    /// <summary>Lấy presigned URL (TTL 5 phút) để hiển thị ảnh evidence.</summary>
    [HttpGet("obligations/{id:int}/evidence/{kind}/download-url")]
    [HasPermission(Permissions.BhxhTaxPaymentReadAll)]
    public async Task<IActionResult> GetEvidenceDownloadUrl(
        int id, string kind, CancellationToken ct)
    {
        if (!TryParseKind(kind, out var parsed))
            return BadRequest(new { errors = new[] { "kind phải là 'bank-receipt' hoặc 'authority-confirmation'." } });

        var url = await _mediator.Send(new GetEvidenceDownloadUrlQuery(id, parsed), ct);
        return url is null ? NotFound() : Ok(new { downloadUrl = url });
    }

    private async Task<IActionResult> UploadEvidenceInternal(
        int obligationId, BhxhTaxEvidenceKind kind, UploadEvidenceRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length <= 0)
            return BadRequest(new { errors = new[] { "file là bắt buộc." } });

        await using var stream = request.File.OpenReadStream();
        var objectKey = await _mediator.Send(new UploadObligationEvidenceCommand(
            ObligationId: obligationId,
            Kind: kind,
            Content: stream,
            ContentType: request.File.ContentType ?? string.Empty,
            SizeBytes: request.File.Length), ct);
        return Ok(new { objectKey });
    }

    private static bool TryParseKind(string kind, out BhxhTaxEvidenceKind parsed)
    {
        switch (kind?.ToLowerInvariant())
        {
            case "bank-receipt":
                parsed = BhxhTaxEvidenceKind.BankReceipt; return true;
            case "authority-confirmation":
                parsed = BhxhTaxEvidenceKind.AuthorityConfirmation; return true;
            default:
                parsed = default; return false;
        }
    }
}
