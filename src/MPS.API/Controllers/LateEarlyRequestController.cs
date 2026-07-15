using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.LateEarlyRequest.Commands;
using MPS.Application.Features.Attendance.LateEarlyRequest.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/late-early-requests")]
[Authorize]
public class LateEarlyRequestController : ControllerBase
{
    private readonly IMediator _mediator;

    public LateEarlyRequestController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.LateEarlyRequestReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeInformationId,
        [FromQuery] LateEarlyRequestStatus? status,
        [FromQuery] LateEarlyKind? kind,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLateEarlyRequestsQuery(
            employeeInformationId, status, kind, fromDate, toDate, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.LateEarlyRequestReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] LateEarlyRequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyLateEarlyRequestsQuery(status, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.LateEarlyRequestReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLateEarlyRequestByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.LateEarlyRequestCreateSelf)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLateEarlyRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateLateEarlyRequestCommand(
            request.WorkDate, request.Kind, request.Minutes, request.Reason,
            request.ApproverRoute, request.OverrideReason), cancellationToken);
        return Created($"/api/late-early-requests/{id}", new { id });
    }

    [HttpPut("{id:int}/approve")]
    [HasPermission(Permissions.LateEarlyRequestApproveAll)]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] ApproveLateEarlyRequestBody request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApproveLateEarlyRequestCommand(
            id, request.ReviewNote, request.DeductFromLeaveBalance, request.OverrideReason), cancellationToken);
        if (!result.Approved)
            return UnprocessableEntity(result);
        return Ok(result);
    }

    [HttpPut("{id:int}/reject")]
    [HasPermission(Permissions.LateEarlyRequestApproveAll)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] RejectLateEarlyRequestBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new RejectLateEarlyRequestCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/cancel")]
    [HasPermission(Permissions.LateEarlyRequestCancelSelf)]
    public async Task<IActionResult> Cancel(
        int id,
        [FromBody] CancelLateEarlyRequestBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new CancelLateEarlyRequestCommand(id, request.CancelReason), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
