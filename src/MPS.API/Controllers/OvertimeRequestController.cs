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

/// <summary>
/// OT workflow v3: NV tự đăng ký → PM (tổ trưởng đơn) review → HR/DM approve.
/// Xem docs/plans/overtime-request-workflow-plan.md.
/// </summary>
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

    // =========================================================================
    // Read endpoints
    // =========================================================================

    [HttpGet]
    [HasPermission(Permissions.OvertimeRequestReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeInformationId,
        [FromQuery] OtRequestStatus? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] bool myScope = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetOvertimeRequestsQuery(
            employeeInformationId, status, fromDate, toDate, page, size,
            MyScope: myScope), cancellationToken);
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
    /// PM xem OT các NV đã submit trong đơn mà PM là tổ trưởng — reuse permission
    /// Review.Project. Legacy endpoint tên `created-by-me` giữ để FE không vỡ.
    /// </summary>
    [HttpGet("created-by-me")]
    [HasPermission(Permissions.OvertimeRequestReviewProject)]
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
    /// PM/HR/DM xem list task NV (người tạo request) được assign trong đơn của request.
    /// Dùng ở detail panel để làm context khi review/duyệt.
    /// </summary>
    [HttpGet("{id:int}/nv-tasks")]
    [HasAnyPermission(
        Permissions.OvertimeRequestReviewProject,
        Permissions.OvertimeRequestApproveHr,
        Permissions.OvertimeRequestApproveDepartment,
        Permissions.OvertimeRequestReadAll)]
    public async Task<IActionResult> GetNvTasks(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOvertimeRequestNvTasksQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Form NV: auto-derive đơn từ task NV đang Status=InProgress. Trả 0 hoặc 1
    /// đơn (theo luật '1 task open'); mỗi đơn kèm task list InProgress + PM name.
    /// </summary>
    [HttpGet("my-active-orders")]
    [HasPermission(Permissions.OvertimeRequestCreateSelf)]
    public async Task<IActionResult> GetMyActiveOrders(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyActiveOrdersForOtQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Giới hạn OT hiện hành (active LaborLawConstants) để form NV hiển thị hint động
    /// (Xh/ngày, Yh/tháng, Zh/năm...). Chỉ expose 4 con số OT + legal reference; không
    /// đụng vào phần lương/BHXH của LaborLaw, nên dùng lại permission CreateSelf.
    /// </summary>
    [HttpGet("limits")]
    [HasPermission(Permissions.OvertimeRequestCreateSelf)]
    public async Task<IActionResult> GetLimits(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOvertimeLimitsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// NV xem tổng giờ OT hôm nay + tháng hiện tại (Approved vs Pending) + cap tương ứng.
    /// Route giữ tên `me/summary/yearly` để FE khỏi vỡ; nội dung đã đơn giản hoá (không còn
    /// breakdown theo năm — chỉ current day + current month).
    /// </summary>
    [HttpGet("me/summary/yearly")]
    [HasPermission(Permissions.OvertimeRequestReadSelf)]
    public async Task<IActionResult> GetMyYearlySummary(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyOvertimeYearlySummaryQuery(), cancellationToken);
        return Ok(result);
    }

    // =========================================================================
    // NV self-service — Create / Update / Cancel
    // =========================================================================

    /// <summary>
    /// NV tự đăng ký OT. Body: projectId (đơn NV đang có task InProgress) + slot + workDate.
    /// Handler validate: đơn Active + có ResponsibleUserId + NV có task InProgress.
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.OvertimeRequestCreateSelf)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOvertimeRequestRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateOvertimeRequestCommand(
            request.ProjectId,
            request.WorkDate,
            request.OtStartTime,
            request.OtEndTime,
            request.Reason), cancellationToken);
        return Ok(new { id });
    }

    /// <summary>NV tự huỷ đơn OT của mình khi còn Pending (chưa PM review).</summary>
    [HttpPut("{id:int}/cancel")]
    [HasPermission(Permissions.OvertimeRequestCreateSelf)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new CancelOvertimeRequestCommand(id), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>NV sửa đơn OT của mình khi còn Pending. Đổi workDate/slot/reason.</summary>
    [HttpPut("{id:int}")]
    [HasPermission(Permissions.OvertimeRequestCreateSelf)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePendingOvertimeRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new UpdatePendingOvertimeRequestCommand(
            id, request.WorkDate, request.OtStartTime, request.OtEndTime, request.Reason),
            cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    // =========================================================================
    // PM tầng 1: Review (Forward / Reject)
    // =========================================================================

    /// <summary>
    /// PM (tổ trưởng đơn) review request. Forward = chuyển tiếp cho HR hoặc DM
    /// (kèm ApproverRoute); Reject = đóng request. Guard: currentUser =
    /// Project.ResponsibleUserId.
    /// </summary>
    [HttpPut("{id:int}/pm-review")]
    [HasPermission(Permissions.OvertimeRequestReviewProject)]
    public async Task<IActionResult> PmReview(
        int id,
        [FromBody] PmReviewOvertimeRequestBody body,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new PmReviewOvertimeRequestCommand(
            id, body.Decision, body.ApproverRoute, body.Note), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    // =========================================================================
    // Approver tầng 2: Approve / Reject (HR hoặc DM tuỳ ApproverRoute)
    // =========================================================================

    /// <summary>
    /// HR hoặc DM duyệt cuối. Handler gate role theo `ApproverRoute` của request.
    /// Endpoint yêu cầu 1 trong 2 permission Approve.Hr | Approve.Department.
    /// </summary>
    [HttpPut("{id:int}/approve")]
    [HasAnyPermission(Permissions.OvertimeRequestApproveHr, Permissions.OvertimeRequestApproveDepartment)]
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
    [HasAnyPermission(Permissions.OvertimeRequestApproveHr, Permissions.OvertimeRequestApproveDepartment)]
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
