using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.Domain.Constants;
using MPS.Infrastructure.BackgroundJobs;

namespace MPS.API.Controllers;

/// <summary>
/// Trigger tay cho job cấp quỹ phép đầu năm <c>[D22]</c>. Dùng khi cần cấp bù (sửa dữ liệu HĐ xong,
/// hoặc server tắt suốt tháng 01) và cho E2E. Chỉ SystemAdmin — cùng chuẩn với
/// <see cref="ContractJobsAdminController"/>.
/// </summary>
[ApiController]
[Route("api/admin/leave-balances/jobs")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class LeaveBalanceJobsAdminController : ControllerBase
{
    private readonly AnnualLeaveGrantJob _annualGrant;

    public LeaveBalanceJobsAdminController(AnnualLeaveGrantJob annualGrant)
    {
        _annualGrant = annualGrant;
    }

    /// <summary>
    /// Cấp quỹ phép năm <paramref name="year"/> (mặc định: năm hiện tại) cho mọi NV còn hợp đồng.
    /// <paramref name="employeeInformationId"/> khác null ⇒ chỉ cấp cho một người.
    /// Chạy lại là an toàn: upsert theo khoá (NV, năm, loại nghỉ) và giữ nguyên số ngày đã dùng.
    /// </summary>
    [HttpPost("annual-grant/run")]
    public async Task<IActionResult> RunAnnualGrant(
        [FromQuery] int? year,
        [FromQuery] int? employeeInformationId,
        CancellationToken cancellationToken)
    {
        var targetYear = year ?? DateTime.UtcNow.AddHours(7).Year;
        if (targetYear < 2000 || targetYear > 2100)
            return BadRequest(new { error = "Năm cấp quỹ phải nằm trong khoảng 2000–2100." });

        var result = await _annualGrant.RunOnceAsync(targetYear, employeeInformationId, cancellationToken);
        if (result is null)
            return Conflict(new { error = "Đang có một lượt cấp quỹ phép chạy dở; thử lại sau." });

        return Ok(result);
    }
}
