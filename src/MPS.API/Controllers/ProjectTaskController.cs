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
    [HasAnyPermission(Permissions.TaskReadAssigned, Permissions.TaskReadDepartment, Permissions.TaskReadFullDepartment, Permissions.TaskReadAll)]
    public async Task<IActionResult> GetMyAssigned([FromQuery] bool includeCancelled, CancellationToken ct)
        => Ok(await _mediator.Send(new TaskGetMyAssignedQuery(includeCancelled), ct));

    [HttpGet("department/{id:int}")]
    [HasAnyPermission(Permissions.TaskReadDepartment, Permissions.TaskReadFullDepartment, Permissions.TaskReadAll)]
    public async Task<IActionResult> GetByDepartment(int id, [FromQuery] bool includeCancelled, CancellationToken ct)
        => Ok(await _mediator.Send(new TaskGetByDepartmentQuery(id, includeCancelled), ct));

    // TaskReadAssigned: công nhân xem trọn cây task của đơn mình tham gia (handler chặn đơn không tham gia).
    [HttpGet("project/{id:int}")]
    [HasAnyPermission(
        Permissions.TaskReadAll,
        Permissions.TaskReadDepartment,
        Permissions.TaskReadFullDepartment,
        Permissions.TaskReadAssigned)]
    public async Task<IActionResult> GetByProject(int id, [FromQuery] bool includeCancelled, CancellationToken ct)
        => Ok(await _mediator.Send(new TaskGetByProjectQuery(id, includeCancelled), ct));

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.TaskReadAssigned, Permissions.TaskReadDepartment, Permissions.TaskReadFullDepartment, Permissions.TaskReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new TaskGetByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}/audit-history")]
    [HasAnyPermission(Permissions.TaskAuditReadAll, Permissions.TaskAuditReadDepartment)]
    public async Task<IActionResult> GetAuditHistory(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new TaskAuditHistoryQuery(id), ct));

    // ⭐ Tạo Công đoạn (IsBatchParent=true, cần TaskCreateStageDepartment) HOẶC Task lá 1 người/nhóm
    // (IsBatchParent=false, cần TaskCreateDepartment) — handler tự soi request.IsBatchParent để check đúng quyền chi tiết.
    [HttpPost]
    [HasAnyPermission(Permissions.TaskCreateDepartment, Permissions.TaskCreateStageDepartment)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new TaskCreateCommand(
            request.ProjectId, request.DepartmentId, request.ParentTaskId, request.Title, request.Description,
            request.ProgressType, request.PlannedQuantity, request.PlannedWorkUnits, request.IsRequiredGate,
            request.Deadline, request.Priority, request.StepOrder, request.AssigneeUserIds,
            request.IsBatchParent, request.PlannedHeadcount, request.RequiredPositionId), ct);
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
            request.PlannedQuantity, request.PlannedWorkUnits, request.IsRequiredGate, request.AssigneeUserIds), ct);
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

    // ⭐ Bấm hoàn thành (quá deadline vẫn Done nhưng gắn WasLate).
    [HttpPatch("{id:int}/complete")]
    [HasPermission(Permissions.TaskCompleteDepartment)]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteTaskRequest request, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskCompleteCommand(id, request.RowVersion, request.CompletedAt), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.TaskDeleteDepartment)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskDeleteCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Pause/Resume/Cancel/Restore (plan mục 6.5) — Planning Manager của xưởng / GĐ (check trong handler) =====

    [HttpPatch("{id:int}/pause")]
    [HasAnyPermission(Permissions.TaskCreateStageDepartment, Permissions.TaskCompleteDepartment)]
    public async Task<IActionResult> Pause(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskPauseCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/resume")]
    [HasAnyPermission(Permissions.TaskCreateStageDepartment, Permissions.TaskCompleteDepartment)]
    public async Task<IActionResult> Resume(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskResumeCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/cancel")]
    [HasAnyPermission(Permissions.TaskCreateStageDepartment, Permissions.TaskCompleteDepartment)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskCancelCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/restore")]
    [HasAnyPermission(Permissions.TaskCreateStageDepartment, Permissions.TaskCompleteDepartment)]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new TaskRestoreCommand(id), ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Attachment (tài liệu/ảnh) =====

    [HttpGet("{id:int}/attachments")]
    [HasAnyPermission(Permissions.ProductionAttachmentReadStage, Permissions.ProductionAttachmentReadAll)]
    public async Task<IActionResult> ListAttachments(int id, [FromQuery] bool completion, CancellationToken ct)
    {
        var ownerType = completion ? AttachmentOwnerType.TaskCompletion : AttachmentOwnerType.Task;
        return Ok(await _attachments.ListAsync(ownerType, id, ct));
    }

    [HttpPost("{id:int}/attachments")]
    [HasAnyPermission(Permissions.ProductionAttachmentWriteStage, Permissions.ProductionAttachmentReadAll)]
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
    [HasAnyPermission(Permissions.ProductionAttachmentWriteStage, Permissions.ProductionAttachmentReadAll)]
    public async Task<IActionResult> DeleteAttachment(int attachmentId, CancellationToken ct)
    {
        await _attachments.DeleteAsync(attachmentId, ct);
        return NoContent();
    }

    // Tải file qua backend (thay vì presigned URL hết hạn 15 phút) — cùng quyền đọc như ListAttachments.
    [HttpGet("attachments/{attachmentId:int}/download")]
    [HasAnyPermission(Permissions.ProductionAttachmentReadStage, Permissions.ProductionAttachmentReadAll)]
    public async Task<IActionResult> DownloadAttachment(int attachmentId, CancellationToken ct)
    {
        var (result, fileName) = await _attachments.DownloadAsync(attachmentId, ct);
        return File(result.Content, result.ContentType, fileName);
    }
}
