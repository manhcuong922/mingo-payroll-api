using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.ProcessTemplate;
using MPS.Application.Features.ProcessTemplate.Commands;
using MPS.Application.Features.ProcessTemplate.Queries;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessTemplateController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProductionAttachmentService _attachments;

    public ProcessTemplateController(IMediator mediator, IProductionAttachmentService attachments)
    {
        _mediator = mediator;
        _attachments = attachments;
    }

    // GetAll/GetById: dùng cả khi quản lý quy trình mẫu (ProcessTemplateReadAll) LẪN khi PM chọn quy trình
    // để "Áp quy trình" cho đơn (ProcessTemplateReadForApply) — 2 quyền tách biệt, xem Permissions.cs.
    [HttpGet]
    [HasAnyPermission(Permissions.ProcessTemplateReadAll, Permissions.ProcessTemplateReadForApply)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new ProcessTemplateGetAllQuery(), ct));

    [HttpGet("paged")]
    [HasPermission(Permissions.ProcessTemplateReadAll)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new ProcessTemplateGetPagedQuery(keyword, page, pageSize), ct));

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.ProcessTemplateReadAll, Permissions.ProcessTemplateReadForApply)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ProcessTemplateGetByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.ProcessTemplateCreateAll)]
    public async Task<IActionResult> Create([FromBody] CreateProcessTemplateRequest request, CancellationToken ct)
    {
        var steps = request.Steps
            .Select(s => new ProcessStepInput(
                s.Name, s.Description, s.StepOrder, s.DefaultProgressType,
                s.DefaultPlannedQuantity, s.DefaultPlannedWorkUnits, s.DefaultIsRequiredGate, s.Priority))
            .ToList();

        var id = await _mediator.Send(
            new ProcessTemplateCreateCommand(request.Code, request.Name, request.Description, request.DefaultDepartmentId, steps), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ProcessTemplateUpdateAll)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProcessTemplateRequest request, CancellationToken ct)
    {
        var steps = request.Steps
            .Select(s => new ProcessStepInput(
                s.Name, s.Description, s.StepOrder, s.DefaultProgressType,
                s.DefaultPlannedQuantity, s.DefaultPlannedWorkUnits, s.DefaultIsRequiredGate, s.Priority))
            .ToList();

        var ok = await _mediator.Send(
            new ProcessTemplateUpdateCommand(id, request.Name, request.Description, request.IsActive, request.DefaultDepartmentId, steps), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ProcessTemplateDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ProcessTemplateDeleteCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Attachment (bản vẽ/hướng dẫn chuẩn của quy trình) =====

    [HttpGet("{id:int}/attachments")]
    [HasPermission(Permissions.ProcessTemplateReadAll)]
    public async Task<IActionResult> ListAttachments(int id, CancellationToken ct)
        => Ok(await _attachments.ListAsync(AttachmentOwnerType.ProcessTemplate, id, ct));

    [HttpPost("{id:int}/attachments")]
    [HasPermission(Permissions.ProcessTemplateCreateAll)]
    public async Task<IActionResult> UploadAttachment(
        int id, IFormFile file, [FromForm] string? note, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File rỗng." });

        await using var stream = file.OpenReadStream();
        var dto = await _attachments.UploadAsync(
            AttachmentOwnerType.ProcessTemplate, id, stream, file.FileName,
            file.ContentType ?? "application/octet-stream", note, ct);
        return Ok(dto);
    }
}
