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

    // year không truyền ⇒ trả quỹ phép MỌI năm (trước đây default int=0 ⇒ trả rỗng, che mất quỹ vừa
    // được hook [D22] cấp — chính là lý do LBAL-HOOK01/#34 đỏ dù quỹ đã có trong DB).
    [HttpGet("{employeeInformationId:int}")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetForEmployee(
        int employeeInformationId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLeaveBalancesQuery(employeeInformationId, year), cancellationToken);
        return Ok(result);
    }

    // /me dựng thêm dòng "quỹ mặc định theo loại phép" nên cần một năm cụ thể ⇒ không truyền thì lấy năm hiện tại.
    [HttpGet("me")]
    [HasPermission(Permissions.AttendanceReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyLeaveBalanceQuery(year ?? DateTime.UtcNow.Year), cancellationToken);
        return Ok(result);
    }
}
