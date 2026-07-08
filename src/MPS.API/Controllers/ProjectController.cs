using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Project;
using MPS.Application.Features.Project.Commands;
using MPS.Application.Features.Project.Queries;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProductionAttachmentService _attachments;

    public ProjectController(IMediator mediator, IProductionAttachmentService attachments)
    {
        _mediator = mediator;
        _attachments = attachments;
    }

    [HttpGet]
    [HasAnyPermission(Permissions.ProjectReadAll, Permissions.ProjectReadDepartment, Permissions.ProjectReadFullDepartment)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeCancelled, [FromQuery] ProjectStatus? status, CancellationToken ct)
        => Ok(await _mediator.Send(new ProjectGetAllQuery(includeCancelled, status), ct));

    [HttpGet("paged")]
    [HasAnyPermission(Permissions.ProjectReadAll, Permissions.ProjectReadDepartment, Permissions.ProjectReadFullDepartment)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int? departmentId,
        [FromQuery] ProjectStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeCancelled = false,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new ProjectGetPagedQuery(keyword, departmentId, page, pageSize, includeCancelled, status), ct));

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.ProjectReadAll, Permissions.ProjectReadDepartment, Permissions.ProjectReadFullDepartment)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ProjectGetByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}/audit-history")]
    [HasAnyPermission(Permissions.TaskAuditReadAll, Permissions.TaskAuditReadDepartment)]
    public async Task<IActionResult> GetAuditHistory(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new ProjectAuditHistoryQuery(id), ct));

    [HttpPost]
    [HasPermission(Permissions.ProjectCreateAll)]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new ProjectCreateCommand(
            request.Title, request.Description, request.StartDate, request.Deadline, request.DepartmentId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:int}/generate-tasks")]
    [HasPermission(Permissions.ProjectGenerateTasks)]
    public async Task<IActionResult> GenerateTasks(int id, [FromBody] GenerateTasksRequest request, CancellationToken ct)
    {
        var count = await _mediator.Send(
            new ProjectGenerateTasksCommand(id, request.ProcessTemplateId, request.Overrides), ct);
        return Ok(new { generatedStages = count });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ProjectUpdateAll)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ProjectUpdateCommand(
            id, request.Title, request.Description, request.Deadline, request.Status), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ProjectDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ProjectDeleteCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    // ⭐ Đóng đơn thủ công (khách xác nhận đủ hàng) + ghi chú rút kinh nghiệm.
    [HttpPatch("{id:int}/complete")]
    [HasPermission(Permissions.ProjectCompleteDepartment)]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteProjectRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ProjectCompleteCommand(id, request.CompletionNote, request.CompletedAt), ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Pause/Resume/Cancel/Restore (plan mục 6.5) — lan truyền xuống toàn bộ Công đoạn + Task con =====

    [HttpPatch("{id:int}/pause")]
    [HasPermission(Permissions.ProjectCompleteDepartment)]
    public async Task<IActionResult> Pause(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ProjectPauseCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/resume")]
    [HasPermission(Permissions.ProjectCompleteDepartment)]
    public async Task<IActionResult> Resume(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ProjectResumeCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/cancel")]
    [HasPermission(Permissions.ProjectCompleteDepartment)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ProjectCancelCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/restore")]
    [HasPermission(Permissions.ProjectCompleteDepartment)]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new ProjectRestoreCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Attachment (tài liệu/ảnh của đơn) =====

    [HttpGet("{id:int}/attachments")]
    [HasAnyPermission(Permissions.ProductionAttachmentReadProject, Permissions.ProductionAttachmentReadAll)]
    public async Task<IActionResult> ListAttachments(int id, CancellationToken ct)
        => Ok(await _attachments.ListAsync(AttachmentOwnerType.Project, id, ct));

    [HttpPost("{id:int}/attachments")]
    [HasAnyPermission(Permissions.ProductionAttachmentWriteProject, Permissions.ProductionAttachmentReadAll)]
    public async Task<IActionResult> UploadAttachment(
        int id, IFormFile file, [FromForm] string? note, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File rỗng." });

        await using var stream = file.OpenReadStream();
        var dto = await _attachments.UploadAsync(
            AttachmentOwnerType.Project, id, stream, file.FileName,
            file.ContentType ?? "application/octet-stream", note, ct);
        return Ok(dto);
    }
}
