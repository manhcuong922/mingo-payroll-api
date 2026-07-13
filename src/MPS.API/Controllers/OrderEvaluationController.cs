using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Evaluation;
using MPS.Application.Common.Interfaces;
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
    private readonly IOrderEvaluationExcelService _excel;

    public OrderEvaluationController(IMediator mediator, IOrderEvaluationExcelService excel)
    {
        _mediator = mediator;
        _excel = excel;
    }

    /// <summary>Danh sách bảng-theo-đơn của tổ trưởng hiện tại (lọc theo trạng thái + phòng ban của đơn).</summary>
    [HttpGet]
    [HasAnyPermission(Permissions.OrderEvaluationReadOwn, Permissions.OrderEvaluationReadAll)]
    public async Task<IActionResult> GetMine(
        [FromQuery] EvaluationStatus? status, [FromQuery] int? departmentId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyOrderEvaluationsQuery(status, departmentId), ct));

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

    /// <summary>Tải Excel mẫu cho 1 bảng-theo-đơn (đã bao chứa rubric + hướng dẫn nghiệp vụ).</summary>
    [HttpGet("{id:int}/template")]
    [HasAnyPermission(Permissions.OrderEvaluationReadOwn, Permissions.OrderEvaluationReadAll)]
    public async Task<IActionResult> DownloadTemplate(int id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetOrderEvaluationByIdQuery(id), ct);
        if (dto is null) return NotFound();

        var bytes = _excel.BuildTemplate(dto);
        var fileName = $"cham-diem-don-{dto.ProjectId}-ky-{dto.PeriodMonth:00}-{dto.PeriodYear}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>Tổ trưởng upload Excel đã điền để chấm hàng loạt (Draft). Trả số dòng điểm đã áp.</summary>
    [HttpPost("{id:int}/import")]
    [HasPermission(Permissions.OrderEvaluationScoreOwn)]
    public async Task<IActionResult> Import(int id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn file Excel để nhập." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var applied = await _mediator.Send(new ImportOrderEvaluationScoresCommand(id, ms.ToArray()), ct);
        return Ok(new { applied });
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
