using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Evaluation;
using MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;
using MPS.Application.Features.Evaluation.MonthlyEvaluation.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonthlyEvaluationController : ControllerBase
{
    private readonly IMediator _mediator;

    public MonthlyEvaluationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>HR/Director xem bảng tháng theo kỳ (+ lọc phòng ban).</summary>
    [HttpGet]
    [HasPermission(Permissions.MonthlyEvaluationReadAll)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int? departmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(
            new GetMonthlyEvaluationsPagedQuery(year, month, departmentId, page, pageSize), ct));

    /// <summary>NV xem điểm tháng của chính mình.</summary>
    [HttpGet("me")]
    [HasPermission(Permissions.MonthlyEvaluationReadSelf)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyMonthlyEvaluationsQuery(), ct));

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.MonthlyEvaluationReadAll, Permissions.MonthlyEvaluationReadSelf)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMonthlyEvaluationByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>HR bấm "Tổng hợp tháng" — gộp bảng-theo-đơn đã chốt thành bảng tháng.</summary>
    [HttpPost("aggregate")]
    [HasPermission(Permissions.MonthlyEvaluationAggregateAll)]
    public async Task<IActionResult> Aggregate([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var updated = await _mediator.Send(new AggregateMonthlyEvaluationCommand(year, month), ct);
        return Ok(new { updated });
    }

    /// <summary>Director tạo bảng tháng Direct cho tổ trưởng/văn phòng.</summary>
    [HttpPost("direct")]
    [HasPermission(Permissions.MonthlyEvaluationScoreDirectAll)]
    public async Task<IActionResult> CreateDirect(
        [FromBody] CreateDirectMonthlyEvaluationRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateDirectMonthlyEvaluationCommand(request.EmployeeUserId, request.Year, request.Month), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>Director cho điểm bảng tháng Direct (Draft).</summary>
    [HttpPut("{id:int}/scores")]
    [HasPermission(Permissions.MonthlyEvaluationScoreDirectAll)]
    public async Task<IActionResult> ScoreDirect(
        int id, [FromBody] ScoreDirectMonthlyEvaluationRequest request, CancellationToken ct)
    {
        var lines = request.Lines
            .Select(l => new DirectScoreLineInput(l.LineId, l.Score, l.Note))
            .ToList();

        var ok = await _mediator.Send(new ScoreDirectMonthlyEvaluationCommand(id, request.RowVersion, lines), ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>HR duyệt + khóa bảng tháng (Draft → Approved).</summary>
    [HttpPost("{id:int}/approve")]
    [HasPermission(Permissions.MonthlyEvaluationApproveAll)]
    public async Task<IActionResult> Approve(
        int id, [FromBody] ApproveMonthlyEvaluationRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ApproveMonthlyEvaluationCommand(id, request.RowVersion), ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>HR mở lại bảng tháng đã khóa (Approved → Draft).</summary>
    [HttpPost("{id:int}/reopen")]
    [HasPermission(Permissions.MonthlyEvaluationReopenAll)]
    public async Task<IActionResult> Reopen(
        int id, [FromBody] ReopenMonthlyEvaluationRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ReopenMonthlyEvaluationCommand(id, request.RowVersion, request.Reason), ct);
        return ok ? NoContent() : NotFound();
    }
}
