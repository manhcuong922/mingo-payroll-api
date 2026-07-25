using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.Domain.Constants;
using MPS.Infrastructure.BackgroundJobs;

namespace MPS.API.Controllers;

/// <summary>
/// Admin manual triggers cho 3 contract background job. Dành cho test/debug + xử lý
/// nếu job lỗi schedule. Chỉ SystemAdmin truy cập được (plan §10.3).
/// </summary>
[ApiController]
[Route("api/admin/contracts/jobs")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class ContractJobsAdminController : ControllerBase
{
    private readonly ContractAutoConvertJob _autoConvert;
    private readonly ContractExpiryJob _expiry;
    private readonly ContractArchiveJob _archive;
    private readonly TerminationAccountDeactivationJob _deactivateTerminated;

    public ContractJobsAdminController(
        ContractAutoConvertJob autoConvert,
        ContractExpiryJob expiry,
        ContractArchiveJob archive,
        TerminationAccountDeactivationJob deactivateTerminated)
    {
        _autoConvert = autoConvert;
        _expiry = expiry;
        _archive = archive;
        _deactivateTerminated = deactivateTerminated;
    }

    [HttpPost("auto-convert/run")]
    public async Task<IActionResult> RunAutoConvert(CancellationToken cancellationToken)
    {
        var count = await _autoConvert.RunOnceAsync(cancellationToken);
        return Ok(new { converted = count });
    }

    [HttpPost("expire/run")]
    public async Task<IActionResult> RunExpiry(CancellationToken cancellationToken)
    {
        var count = await _expiry.RunOnceAsync(cancellationToken);
        return Ok(new { expired = count });
    }

    [HttpPost("archive/run")]
    public async Task<IActionResult> RunArchive(CancellationToken cancellationToken)
    {
        var count = await _archive.RunOnceAsync(cancellationToken);
        return Ok(new { archived = count });
    }

    /// <summary>Case 7 — vô hiệu hoá tài khoản NV có HĐ đã chấm dứt tới hạn, không còn HĐ Active.</summary>
    [HttpPost("deactivate-terminated/run")]
    public async Task<IActionResult> RunDeactivateTerminated(CancellationToken cancellationToken)
    {
        var count = await _deactivateTerminated.RunOnceAsync(cancellationToken);
        return Ok(new { deactivated = count });
    }
}
