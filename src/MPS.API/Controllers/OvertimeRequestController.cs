using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Application.Features.Attendance.OvertimeRequest.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/overtime-requests")]
[Authorize]
public class OvertimeRequestController : ControllerBase
{
    private readonly IMediator _mediator;

    public OvertimeRequestController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.OvertimeRequestReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeInformationId,
        [FromQuery] OtRequestStatus? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetOvertimeRequestsQuery(
            employeeInformationId, status, fromDate, toDate, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.OvertimeRequestReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] OtRequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyOvertimeRequestsQuery(status, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.OvertimeRequestReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOvertimeRequestByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.OvertimeRequestCreateSelf)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOvertimeRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateOvertimeRequestCommand(
            request.WorkDate, request.OtStartTime, request.OtEndTime, request.Reason), cancellationToken);
        return Created($"/api/overtime-requests/{id}", new { id });
    }

    [HttpPut("{id:int}/approve")]
    [HasPermission(Permissions.OvertimeRequestApproveAll)]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] ReviewOvertimeRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new ApproveOvertimeRequestCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/reject")]
    [HasPermission(Permissions.OvertimeRequestApproveAll)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] ReviewOvertimeRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new RejectOvertimeRequestCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
