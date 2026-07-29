using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Payroll;
using MPS.Application.Features.Payroll.Commands;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/payroll")]
public class PayrollController : ControllerBase
{
    private readonly IMediator _mediator;
    public PayrollController(IMediator mediator) => _mediator = mediator;

    /// <summary>Preview đủ liệu trước khi tạo (danh sách NV + thiếu gì).</summary>
    [HttpGet("periods/readiness")]
    [HasPermission(Permissions.PayrollReadAll)]
    public async Task<IActionResult> GetReadiness(
        [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollReadinessQuery(year, month), ct));

    [HttpGet("periods")]
    [HasPermission(Permissions.PayrollReadAll)]
    public async Task<IActionResult> GetPeriods(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollPeriodsQuery(year, month), ct));

    [HttpGet("periods/{id:int}")]
    [HasPermission(Permissions.PayrollReadAll)]
    public async Task<IActionResult> GetSheet(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollSheetQuery(id), ct));

    /// <summary>
    /// Tải bảng lương .xlsx — công thức Excel thật (bấm vào ô là thấy, sửa input Excel tự tính lại),
    /// kèm sheet tham số (trần/tỷ lệ/biểu thuế) và sheet ghi chú (trần + thay đổi luật so kỳ trước).
    /// </summary>
    [HttpGet("periods/{id:int}/export")]
    [HasPermission(Permissions.PayrollReadAll)]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var file = await _mediator.Send(new ExportPayrollPeriodQuery(id), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>NV xem phiếu lương của CHÍNH MÌNH (kỳ đã chốt), mặc định 6 kỳ gần nhất.</summary>
    [HttpGet("me/payslips")]
    [HasPermission(Permissions.PayrollReadSelf)]
    public async Task<IActionResult> GetMyPayslips([FromQuery] int months, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyPayslipsQuery(months), ct));

    /// <summary>
    /// NV xem chi tiết 1 phiếu lương của CHÍNH MÌNH: metadata kỳ, danh sách cột (label/nhóm/thứ tự)
    /// và các ô của riêng NV. 404 nếu kỳ Draft, không tồn tại, hoặc NV không có dòng trong kỳ.
    /// </summary>
    [HttpGet("me/payslips/{periodId:int}")]
    [HasPermission(Permissions.PayrollReadSelf)]
    public async Task<IActionResult> GetMyPayslipDetail(int periodId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyPayslipDetailQuery(periodId), ct));

    /// <summary>
    /// NV tải phiếu lương .xlsx của CHÍNH MÌNH — sheet "4. LUONG" chỉ dòng của NV, kèm biểu thuế
    /// TNCN + tỷ lệ BHXH + ghi chú làm căn cứ. 404 nếu kỳ Draft, không tồn tại hoặc NV không có dòng.
    /// </summary>
    [HttpGet("me/payslips/{periodId:int}/export")]
    [HasPermission(Permissions.PayrollReadSelf)]
    public async Task<IActionResult> ExportMyPayslip(int periodId, CancellationToken ct)
    {
        var file = await _mediator.Send(new ExportMyPayslipQuery(periodId), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Sinh bảng lương tháng. Thiếu liệu → 422 kèm danh sách NV thiếu.</summary>
    [HttpPost("periods")]
    [HasPermission(Permissions.PayrollCreateAll)]
    public async Task<IActionResult> Create([FromBody] CreatePayrollPeriodRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreatePayrollPeriodCommand(
            request.Year, request.Month, request.FormulaSetId, request.StandardWorkingDays), ct);
        return CreatedAtAction(nameof(GetSheet), new { id }, new { id });
    }

    /// <summary>Sửa tay 1 ô (override, ghim) — OverrideValue=null để bỏ ghim; tính lại downstream + tổng.</summary>
    [HttpPut("periods/{id:int}/cells")]
    [HasPermission(Permissions.PayrollUpdateAll)]
    public async Task<IActionResult> EditCell(int id, [FromBody] EditPayrollCellRequest request, CancellationToken ct)
    {
        await _mediator.Send(new EditPayrollCellCommand(
            id, request.RowId, request.ColumnCode, request.OverrideValue, request.OverrideText), ct);
        return NoContent();
    }

    /// <summary>
    /// BS-6: tính lại kỳ Draft từ nguồn mới (khi IsStale=true), giữ nguyên các ô đã ghim tay.
    /// Kỳ đã chốt hoặc thiếu liệu → chặn.
    /// </summary>
    [HttpPost("periods/{id:int}/recalculate")]
    [HasPermission(Permissions.PayrollUpdateAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Recalculate(int id, CancellationToken ct)
    {
        await _mediator.Send(new RecalculatePayrollPeriodCommand(id), ct);
        return NoContent();
    }

    // #11: Swagger trước đây khai 200 (mặc định của IActionResult) trong khi thực tế trả 204.
    [HttpPost("periods/{id:int}/finalize")]
    [HasPermission(Permissions.PayrollFinalizeAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Finalize(int id, CancellationToken ct)
    {
        await _mediator.Send(new FinalizePayrollPeriodCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Khoá kỳ (Finalized → Locked). Sự kiện <c>PayrollPeriodLockedNotification</c> được publish
    /// sau khi lưu để handler tự sinh 2 nghĩa vụ nộp BHXH & TNCN vào module Payment Tracking.
    /// </summary>
    [HttpPost("periods/{id:int}/lock")]
    [HasPermission(Permissions.PayrollLockAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Lock(int id, CancellationToken ct)
    {
        await _mediator.Send(new LockPayrollPeriodCommand(id), ct);
        return NoContent();
    }

    [HttpDelete("periods/{id:int}")]
    [HasPermission(Permissions.PayrollDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _mediator.Send(new DeletePayrollPeriodCommand(id), ct);
        return NoContent();
    }
}
