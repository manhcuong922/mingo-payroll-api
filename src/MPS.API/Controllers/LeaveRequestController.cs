using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.LeaveRequest.Commands;
using MPS.Application.Features.Attendance.LeaveRequest.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/leave-requests")]
[Authorize]
public class LeaveRequestController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeaveRequestController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.LeaveRequestReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeInformationId,
        [FromQuery] LeaveRequestStatus? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLeaveRequestsQuery(
            employeeInformationId, status, fromDate, toDate, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary/yearly")]
    [HasPermission(Permissions.LeaveRequestReadAll)]
    public async Task<IActionResult> GetYearlySummary(
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        if (year < 1900 || year > 9999)
            return BadRequest(new { error = "Year must be between 1900 and 9999." });

        var result = await _mediator.Send(new GetEmployeeLeaveYearlySummaryQuery(year), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.LeaveRequestReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] LeaveRequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyLeaveRequestsQuery(status, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.LeaveRequestReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLeaveRequestByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.LeaveRequestCreateSelf)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLeaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateLeaveRequestCommand(
            request.LeaveTypeId,
            request.StartDate,
            request.EndDate,
            request.Reason,
            request.SupportingDocumentUrl,
            request.MedicalCertificateUrl,
            request.InsuranceCertNumber,
            request.ApproverRoute,
            request.OverrideReason), cancellationToken);
        return Created($"/api/leave-requests/{result.Id}", result);
    }

    [HttpPut("{id:int}/approve")]
    [HasPermission(Permissions.LeaveRequestApproveAll)]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] ApproveLeaveRequestBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new ApproveLeaveRequestCommand(
            id,
            request.ReviewNote,
            request.MedicalCertificateUrl,
            request.InsuranceCertNumber,
            request.OverrideReason), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/reject")]
    [HasPermission(Permissions.LeaveRequestApproveAll)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] ReviewLeaveRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new RejectLeaveRequestCommand(id, request.ReviewNote), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/cancel")]
    [HasPermission(Permissions.LeaveRequestCancelSelf)]
    public async Task<IActionResult> Cancel(
        int id,
        [FromBody] CancelLeaveRequestBody request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new CancelLeaveRequestCommand(id, request.CancelReason), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
