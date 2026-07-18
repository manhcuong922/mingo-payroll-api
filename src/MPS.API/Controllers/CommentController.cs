using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Comment;
using MPS.Application.Features.Comment.Commands;
using MPS.Application.Features.Comment.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/Comment")]
public class CommentController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // Quyền đọc/viết chi tiết (Manager/PM theo scope, GĐ mọi tầng, Employee chỉ task của mình)
    // được kiểm trong handler — attribute ở đây chỉ chặn user hoàn toàn không có quyền nào liên quan Comment/Task/Project.
    [HttpGet]
    [HasAnyPermission(
        Permissions.CommentReadAll, Permissions.ProjectReadAll, Permissions.ProjectReadDepartment,
        Permissions.TaskReadDepartment, Permissions.TaskReadAssigned)]
    public async Task<IActionResult> GetByOwner([FromQuery] CommentOwnerType ownerType, [FromQuery] int ownerId, CancellationToken ct)
        => Ok(await _mediator.Send(new CommentGetByOwnerQuery(ownerType, ownerId), ct));

    [HttpPost]
    [HasAnyPermission(
        Permissions.CommentCreateOwn, Permissions.CommentCreateDepartment, Permissions.ProjectReadAll,
        Permissions.ProjectCompleteDepartment, Permissions.TaskCreateStageDepartment, Permissions.TaskCompleteDepartment)]
    public async Task<IActionResult> Create([FromBody] CreateCommentRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CommentCreateCommand(request.OwnerType, request.OwnerId, request.Content), ct);
        return CreatedAtAction(nameof(GetByOwner), new { ownerType = request.OwnerType, ownerId = request.OwnerId }, new { id });
    }
}
