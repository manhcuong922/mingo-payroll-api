using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.LeaveBalance.Commands;
using MPS.Application.Features.Attendance.LeaveBalance.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/leave-balances")]
[Authorize]
public class LeaveBalanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeaveBalanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [HasPermission(Permissions.AttendanceCreateAll)]
    public async Task<IActionResult> Init(
        [FromBody] InitLeaveBalanceRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new InitLeaveBalanceCommand(
            request.EmployeeInformationId, request.Year, request.LeaveTypeId), cancellationToken);
        return Created($"/api/leave-balances/{request.EmployeeInformationId}", new { id });
    }

    [HttpGet("{employeeInformationId:int}")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetForEmployee(
        int employeeInformationId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLeaveBalancesQuery(employeeInformationId, year), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.AttendanceReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyLeaveBalanceQuery(year), cancellationToken);
        return Ok(result);
    }
}
