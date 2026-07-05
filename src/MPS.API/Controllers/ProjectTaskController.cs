using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.ProjectTask;
using MPS.Application.Features.ProjectTask.Commands;
using MPS.Application.Features.ProjectTask.Queries;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/Task")]
public class ProjectTaskController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProductionAttachmentService _attachments;

    public ProjectTaskController(IMediator mediator, IProductionAttachmentService attachments)
    {
        _mediator = mediator;
        _attachments = attachments;
    }

    [HttpGet("my")]
    [HasAnyPermission(Permissions.TaskReadAssigned, Permissions.TaskReadDepartment, Permissions.TaskReadAll)]
    public async Task<IActionResult> GetMyAssigned(CancellationToken ct)
        => Ok(await _mediator.Send(new TaskGetMyAssignedQuery(), ct));

    [HttpGet("department/{id:int}")]
    [HasAnyPermission(Permissions.TaskReadDepartment, Permissions.TaskReadAll)]
    public async Task<IActionResult> GetByDepartment(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new TaskGetByDepartmentQuery(id), ct));

    [HttpGet("project/{id:int}")]
    [HasAnyPermission(Permissions.TaskReadAll, Permissions.TaskReadDepartment)]
    public async Task<IActionResult> GetByProject(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new TaskGetByProjectQuery(id), ct));

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.TaskReadAssigned, Permissions.TaskReadDepartment, Permissions.TaskReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new TaskGetByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}/audit-history")]
    [HasAnyPermission(Permissions.TaskAuditReadAll, Permissions.TaskAuditReadDepartment)]
    public async Task<IActionResult> GetAuditHistory(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new TaskAuditHistoryQuery(id), ct));

    // ⭐ Tạo task 1 người / nhóm (AssigneeUserIds nhiều phần tử = nhóm).
    [HttpPost]
    [HasPermission(Permissions.TaskCreateDepartment)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new TaskCreateCommand(
            request.ProjectId, request.DepartmentId, request.ParentTaskId, request.Title, request.Description,
            request.ProgressType, request.PlannedQuantity, request.Weight,
            request.RequiresHandover, request.Deadline, request.Priority, request.StepOrder, request.AssigneeUserIds,
            request.IsBatchParent), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("confidential")]
    [HasPermission(Permissions.TaskCreateConfidential)]
    public async Task<IActionResult> CreateConfidential([FromBody] CreateConfidentialTaskRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new TaskCreateConfidentialCommand(
            request.Title, request.Description, request.AssigneeUserIds, request.Deadline, request.Priority), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.TaskUpdateDepartment)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskUpdateCommand(
            id, request.Title, request.Description, request.Deadline, request.Priority,
            request.PlannedQuantity, request.Weight, request.AssigneeUserIds), ct);
        return ok ? NoContent() : NotFound();
    }

    // ⭐ Cập nhật sản lượng (task Quantity) — không ép hàng ngày.
    [HttpPatch("{id:int}/progress")]
    [HasPermission(Permissions.TaskUpdateProgressDepartment)]
    public async Task<IActionResult> UpdateProgress(int id, [FromBody] UpdateTaskProgressRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskUpdateProgressCommand(id, request.CompletedQuantity, request.RowVersion), ct);
        return ok ? NoContent() : NotFound();
    }

    // ⭐ Bấm hoàn thành (quá deadline vẫn Done nhưng gắn WasLate; bàn giao bắt buộc attachment).
    [HttpPatch("{id:int}/complete")]
    [HasPermission(Permissions.TaskCompleteDepartment)]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteTaskRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskCompleteCommand(id, request.RowVersion), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.TaskDeleteDepartment)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskDeleteCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Attachment (tài liệu/ảnh) =====

    [HttpGet("{id:int}/attachments")]
    [HasAnyPermission(Permissions.ProductionAttachmentReadDepartment, Permissions.ProductionAttachmentReadAll)]
    public async Task<IActionResult> ListAttachments(int id, [FromQuery] bool completion, CancellationToken ct)
    {
        var ownerType = completion ? AttachmentOwnerType.TaskCompletion : AttachmentOwnerType.Task;
        return Ok(await _attachments.ListAsync(ownerType, id, ct));
    }

    // completion=true → bằng chứng nghiệm thu (gắn trước khi Complete task bàn giao).
    [HttpPost("{id:int}/attachments")]
    [HasPermission(Permissions.ProductionAttachmentWriteDepartment)]
    public async Task<IActionResult> UploadAttachment(
        int id, IFormFile file, [FromForm] string? note, [FromForm] bool completion, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File rỗng." });

        var ownerType = completion ? AttachmentOwnerType.TaskCompletion : AttachmentOwnerType.Task;
        await using var stream = file.OpenReadStream();
        var dto = await _attachments.UploadAsync(
            ownerType, id, stream, file.FileName, file.ContentType ?? "application/octet-stream", note, ct);
        return Ok(dto);
    }

    [HttpDelete("attachments/{attachmentId:int}")]
    [HasPermission(Permissions.ProductionAttachmentWriteDepartment)]
    public async Task<IActionResult> DeleteAttachment(int attachmentId, CancellationToken ct)
    {
        await _attachments.DeleteAsync(attachmentId, ct);
        return NoContent();
    }
}
