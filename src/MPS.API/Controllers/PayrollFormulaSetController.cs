using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Payroll;
using MPS.Application.Features.Payroll.Commands;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

/// <summary>Bảng công thức lương (CRUD + clone + cột) cho kế toán tự cấu hình.</summary>
[ApiController]
[Route("api/payroll/formula-sets")]
public class PayrollFormulaSetController : ControllerBase
{
    private readonly IMediator _mediator;
    public PayrollFormulaSetController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(Permissions.PayrollFormulaSetReadAll)]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFormulaSetsQuery(isActive), ct));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.PayrollFormulaSetReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFormulaSetByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(Permissions.PayrollFormulaSetCreateAll)]
    public async Task<IActionResult> Create([FromBody] CreateFormulaSetRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateFormulaSetCommand(request.Name, request.Description), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.PayrollFormulaSetUpdateAll)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFormulaSetRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateFormulaSetCommand(id, request.Name, request.Description, request.IsActive), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.PayrollFormulaSetDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteFormulaSetCommand(id), ct);
        return NoContent();
    }

    /// <summary>Nhân bản bảng (thường clone bảng mặc định rồi tùy chỉnh).</summary>
    [HttpPost("{id:int}/clone")]
    [HasPermission(Permissions.PayrollFormulaSetCreateAll)]
    public async Task<IActionResult> Clone(int id, [FromBody] CloneFormulaSetRequest request, CancellationToken ct)
    {
        var newId = await _mediator.Send(new CloneFormulaSetCommand(id, request.NewName, request.Description), ct);
        return CreatedAtAction(nameof(GetById), new { id = newId }, new { id = newId });
    }

    [HttpPost("{id:int}/set-default")]
    [HasPermission(Permissions.PayrollFormulaSetUpdateAll)]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        await _mediator.Send(new SetDefaultFormulaSetCommand(id), ct);
        return NoContent();
    }

    // ===== Cột =====
    [HttpPost("{id:int}/columns")]
    [HasPermission(Permissions.PayrollFormulaSetUpdateAll)]
    public async Task<IActionResult> AddColumn(int id, [FromBody] AddColumnRequest request, CancellationToken ct)
    {
        var columnId = await _mediator.Send(new AddColumnCommand(
            id, request.ColumnCode, request.Label, request.GroupName, request.DisplayOrder,
            request.DataType, request.ValueSource, request.LookupKey, request.FormulaJson,
            request.Rounding, request.IsMoney, request.IsEditable), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { columnId });
    }

    [HttpPut("columns/{columnId:int}")]
    [HasPermission(Permissions.PayrollFormulaSetUpdateAll)]
    public async Task<IActionResult> UpdateColumn(int columnId, [FromBody] UpdateColumnRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateColumnCommand(
            columnId, request.Label, request.GroupName, request.DisplayOrder,
            request.DataType, request.ValueSource, request.LookupKey, request.FormulaJson,
            request.Rounding, request.IsMoney, request.IsEditable), ct);
        return NoContent();
    }

    [HttpDelete("columns/{columnId:int}")]
    [HasPermission(Permissions.PayrollFormulaSetUpdateAll)]
    public async Task<IActionResult> DeleteColumn(int columnId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteColumnCommand(columnId), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/columns/order")]
    [HasPermission(Permissions.PayrollFormulaSetUpdateAll)]
    public async Task<IActionResult> ReorderColumns(int id, [FromBody] ReorderColumnsRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ReorderColumnsCommand(id, request.OrderedColumnIds), ct);
        return NoContent();
    }
}
