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
        [FromQuery] int employerPartyId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollReadinessQuery(employerPartyId, year, month), ct));

    [HttpGet("periods")]
    [HasPermission(Permissions.PayrollReadAll)]
    public async Task<IActionResult> GetPeriods(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollPeriodsQuery(year, month), ct));

    [HttpGet("periods/{id:int}")]
    [HasPermission(Permissions.PayrollReadAll)]
    public async Task<IActionResult> GetSheet(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollSheetQuery(id), ct));

    /// <summary>Sinh bảng lương tháng. Thiếu liệu → 422 kèm danh sách NV thiếu.</summary>
    [HttpPost("periods")]
    [HasPermission(Permissions.PayrollCreateAll)]
    public async Task<IActionResult> Create([FromBody] CreatePayrollPeriodRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreatePayrollPeriodCommand(
            request.EmployerPartyId, request.Year, request.Month, request.FormulaSetId, request.StandardWorkingDays), ct);
        return CreatedAtAction(nameof(GetSheet), new { id }, new { id });
    }

    /// <summary>Sửa tay 1 ô (override, ghim) — OverrideValue=null để bỏ ghim; tính lại downstream + tổng.</summary>
    [HttpPut("periods/{id:int}/cells")]
    [HasPermission(Permissions.PayrollUpdateAll)]
    public async Task<IActionResult> EditCell(int id, [FromBody] EditPayrollCellRequest request, CancellationToken ct)
    {
        await _mediator.Send(new EditPayrollCellCommand(id, request.RowId, request.ColumnCode, request.OverrideValue), ct);
        return NoContent();
    }

    [HttpPost("periods/{id:int}/finalize")]
    [HasPermission(Permissions.PayrollFinalizeAll)]
    public async Task<IActionResult> Finalize(int id, CancellationToken ct)
    {
        await _mediator.Send(new FinalizePayrollPeriodCommand(id), ct);
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
