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

    /// <summary>
    /// PM xem OT do chính PM đã gửi. Không cần ReadAll — reuse permission CreateBatchDepartment
    /// vì ai giao được OT thì phải xem được đơn của mình gửi.
    /// </summary>
    [HttpGet("created-by-me")]
    [HasPermission(Permissions.OvertimeRequestCreateBatchDepartment)]
    public async Task<IActionResult> GetCreatedByMe(
        [FromQuery] OtRequestStatus? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? projectId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetOvertimeRequestsCreatedByMeQuery(status, fromDate, toDate, projectId, page, size),
            cancellationToken);
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

    /// <summary>
    /// PM tạo batch OT request cho N nhân viên cùng project. Partial success —
    /// 1 NV fail (vượt trần OT hoặc scope không match) sẽ bỏ NV đó, các NV còn
    /// lại vẫn tạo. Kết quả chi tiết ở response.
    /// </summary>
    [HttpPost("batch")]
    [HasPermission(Permissions.OvertimeRequestCreateBatchDepartment)]
    public async Task<IActionResult> CreateBatch(
        [FromBody] CreateBatchOvertimeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateBatchOvertimeRequestCommand(
            request.ProjectId,
            request.WorkDate,
            request.OtStartTime,
            request.OtEndTime,
            request.EmployeeInformationIds,
            request.ApproverRoute,
            request.Reason), cancellationToken);
        return Ok(result);
    }

    /// <summary>List Project thuộc Department của PM — populate dropdown ở form batch.</summary>
    [HttpGet("pm-projects")]
    [HasPermission(Permissions.OvertimeRequestCreateBatchDepartment)]
    public async Task<IActionResult> GetPmProjects(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPmProjectsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>List NV thuộc Department của Project — populate picker ở form batch.</summary>
    [HttpGet("eligible-employees")]
    [HasPermission(Permissions.OvertimeRequestCreateBatchDepartment)]
    public async Task<IActionResult> GetEligibleEmployees(
        [FromQuery] int projectId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetEligibleEmployeesForOvertimeQuery(projectId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PM huỷ đơn OT của chính mình khi còn Pending. Handler enforce CreatedByUserId=current
    /// nên endpoint chỉ cần permission tạo batch (PM đã có sẵn).
    /// </summary>
    [HttpPut("{id:int}/cancel")]
    [HasPermission(Permissions.OvertimeRequestCreateBatchDepartment)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new CancelOvertimeRequestCommand(id), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// PM sửa đơn OT của chính mình khi còn Pending. Đổi được workDate/otStart/otEnd/reason/approverRoute.
    /// Không đổi được nhân viên hay project (huỷ + tạo mới nếu cần).
    /// </summary>
    [HttpPut("{id:int}")]
    [HasPermission(Permissions.OvertimeRequestCreateBatchDepartment)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePendingOvertimeRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new UpdatePendingOvertimeRequestCommand(
            id, request.WorkDate, request.OtStartTime, request.OtEndTime,
            request.ApproverRoute, request.Reason), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
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
