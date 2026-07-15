using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.WfhRequest.Commands;
using MPS.Application.Features.Attendance.WfhRequest.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/wfh-requests")]
[Authorize]
public class WfhRequestController : ControllerBase
{
    private readonly IMediator _mediator;

    public WfhRequestController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.WfhRequestReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeInformationId,
        [FromQuery] WfhRequestStatus? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetWfhRequestsQuery(
            employeeInformationId, status, fromDate, toDate, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.WfhRequestReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] WfhRequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyWfhRequestsQuery(status, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.WfhRequestReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWfhRequestByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.WfhRequestCreateSelf)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWfhRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateWfhRequestCommand(
            request.StartDate, request.EndDate, request.WorkPlan, request.Reason,
            request.ApproverRoute, request.OverrideReason), cancellationToken);
        return Created($"/api/wfh-requests/{id}", new { id });
    }

    [HttpPut("{id:int}/approve")]
    [HasPermission(Permissions.WfhRequestApproveAll)]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] ApproveWfhRequestBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new ApproveWfhRequestCommand(
            id, request.ReviewNote, request.OverrideReason), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/reject")]
    [HasPermission(Permissions.WfhRequestApproveAll)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] RejectWfhRequestBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new RejectWfhRequestCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/cancel")]
    [HasPermission(Permissions.WfhRequestCancelSelf)]
    public async Task<IActionResult> Cancel(
        int id,
        [FromBody] CancelWfhRequestBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new CancelWfhRequestCommand(id, request.CancelReason), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
