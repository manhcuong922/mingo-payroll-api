using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.Application.Features.AuditLog.Queries.GetRecordHistory;
using MPS.Application.Features.AuditLog.Queries.QueryAuditLogs;
using MPS.Domain.Constants;
using MPS.Domain.Enums;

namespace MPS.API.Controllers;

/// <summary>
/// FT-39 — Audit log read API. Append-only (interceptor ghi), không expose PUT/DELETE.
/// Mọi nỗ lực sửa/xóa qua route khác sẽ trả 404/405 — phù hợp spec "ghi chú: cố sửa → 403/404".
/// </summary>
[ApiController]
[Authorize]
[Route("api/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.AuditLogReadAll)]
    public async Task<IActionResult> Query(
        [FromQuery] int? userId,
        [FromQuery] string? module,
        [FromQuery] AuditAction? action,
        [FromQuery] AuditSeverity? severity,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new QueryAuditLogsQuery(userId, module, action, severity, fromUtc, toUtc, page, size),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("records/{module}/{recordId}")]
    [HasPermission(Permissions.AuditLogReadAll)]
    public async Task<IActionResult> GetRecordHistory(
        string module,
        string recordId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetRecordHistoryQuery(module, recordId),
            cancellationToken);
        return Ok(result);
    }
}
