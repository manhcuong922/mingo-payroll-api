using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Evaluation;
using MPS.Application.Features.Evaluation.OrderEvaluation.Commands;
using MPS.Application.Features.Evaluation.OrderEvaluation.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Evaluation;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderEvaluationController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrderEvaluationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Danh sách bảng-theo-đơn của tổ trưởng hiện tại (lọc theo trạng thái).</summary>
    [HttpGet]
    [HasAnyPermission(Permissions.OrderEvaluationReadOwn, Permissions.OrderEvaluationReadAll)]
    public async Task<IActionResult> GetMine([FromQuery] EvaluationStatus? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyOrderEvaluationsQuery(status), ct));

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.OrderEvaluationReadOwn, Permissions.OrderEvaluationReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderEvaluationByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Tổ trưởng chấm điểm bảng-theo-đơn (Draft).</summary>
    [HttpPut("{id:int}/scores")]
    [HasPermission(Permissions.OrderEvaluationScoreOwn)]
    public async Task<IActionResult> Score(int id, [FromBody] ScoreOrderEvaluationRequest request, CancellationToken ct)
    {
        var subjects = request.Subjects
            .Select(s => new ScoreSubjectInput(
                s.SubjectId, s.Note,
                s.Lines.Select(l => new ScoreLineInput(l.LineId, l.Score, l.Note)).ToList()))
            .ToList();

        var ok = await _mediator.Send(new ScoreOrderEvaluationCommand(id, request.RowVersion, subjects), ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Tổ trưởng chốt bảng-theo-đơn (Draft → Submitted).</summary>
    [HttpPost("{id:int}/submit")]
    [HasPermission(Permissions.OrderEvaluationSubmitOwn)]
    public async Task<IActionResult> Submit(int id, [FromBody] SubmitOrderEvaluationRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new SubmitOrderEvaluationCommand(id, request.RowVersion), ct);
        return ok ? NoContent() : NotFound();
    }
}
