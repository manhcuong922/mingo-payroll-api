using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.Attendance.Commands;
using MPS.Application.Features.Attendance.Attendance.Queries;
using MPS.Application.Features.Attendance.AttendanceSetting.Commands;
using MPS.Application.Features.Attendance.AttendanceSetting.Queries;
using MPS.Application.Features.Attendance.AttendanceSheet.Commands;
using MPS.Application.Features.Attendance.AttendanceSheet.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public AttendanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] int? employeeInformationId,
        [FromQuery] int? departmentId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetAttendanceSummaryQuery(fromDate, toDate, employeeInformationId, departmentId, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{employeeInformationId:int}")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetEmployeeDetail(
        int employeeInformationId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetEmployeeAttendanceDetailQuery(employeeInformationId, fromDate, toDate), cancellationToken);
        return Ok(result);
    }

    [HttpGet("daily/{workDate}")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetDaily(DateOnly workDate, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDailyAttendanceQuery(workDate), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Toàn bộ AttendanceRecord trong khoảng ngày — flat list, FE tự group cho matrix Bảng chấm công tháng.
    /// </summary>
    [HttpGet("range")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetByRange(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAttendanceByRangeQuery(fromDate, toDate), cancellationToken);
        return Ok(result);
    }

    /// <summary>OT Approved chưa được chấm công cho ngày X — HR click từng row để chấm.</summary>
    [HttpGet("daily/{workDate}/pending-overtime")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetPendingOvertime(DateOnly workDate, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPendingOvertimeAttendanceQuery(workDate), cancellationToken);
        return Ok(result);
    }

    [HttpGet("settings")]
    [HasPermission(Permissions.AttendanceSettingReadAll)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAttendanceSettingQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("settings")]
    [HasPermission(Permissions.AttendanceSettingUpdateAll)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateAttendanceSettingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateAttendanceSettingCommand(
            request.IsEditLockEnabled,
            request.DailyEditCutoffTime,
            request.LockAfterDays,
            request.AllowAdminOverride,
            request.RequireOverrideReason,
            request.LateEarlyMaxOccurrencesPerMonth,
            request.LateEarlyMaxMinutesPerOccurrence,
            request.LateEarlyBackdateDaysAllowed,
            request.WfhMaxDaysPerMonth,
            request.WfhMinNoticeDays,
            request.WfhMaxDaysPerRequest,
            request.WfhWorkPlanMinLength,
            request.WeeklyRestDayMask,
            request.LateGraceMinutes,
            request.EarlyLeaveGraceMinutes,
            request.HalfDayThresholdMinutes), cancellationToken);
        return Ok(result);
    }

    [HttpGet("settings/lock-status")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> GetLockStatus(
        [FromQuery] DateOnly workDate,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAttendanceLockStatusQuery(workDate), cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [HasPermission(Permissions.AttendanceReadSelf)]
    public async Task<IActionResult> GetMine(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyAttendanceQuery(fromDate, toDate), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Hôm nay user có nghỉ không (nghỉ phép đã duyệt / ngày lễ / cuối tuần không có ca).
    /// Kiosk chấm công dùng để hiện thông báo và chặn vào trang quét khuôn mặt.
    /// </summary>
    [HttpGet("me/day-off")]
    [HasPermission(Permissions.AttendanceReadSelf)]
    public async Task<IActionResult> GetMyDayOff(
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyDayOffStatusQuery(date), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.AttendanceCreateAll)]
    public async Task<IActionResult> Record(
        [FromBody] RecordAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new RecordAttendanceCommand(
            request.EmployeeInformationId, request.WorkDate,
            request.ShiftTemplateId, request.OvertimeRequestId,
            request.CheckInAt, request.CheckOutAt,
            request.Status, request.Notes, request.OverrideReason), cancellationToken);
        return Created($"/api/attendance/{request.EmployeeInformationId}/{request.WorkDate:yyyy-MM-dd}", new { id });
    }

    [HttpPost("bulk")]
    [HasPermission(Permissions.AttendanceCreateAll)]
    public async Task<IActionResult> BulkRecord(
        [FromBody] BulkRecordAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var rows = request.Records.Select(r => new BulkRecordAttendanceItem(
            r.EmployeeInformationId, r.ShiftTemplateId, r.CheckInAt, r.CheckOutAt,
            r.Status, r.Notes, r.OverrideReason)).ToList();
        var result = await _mediator.Send(
            new BulkRecordAttendanceCommand(request.WorkDate, rows),
            cancellationToken);
        return Ok(result);
    }

    [HttpPut("{employeeInformationId:int}/{workDate}/{shiftTemplateId:int}")]
    [HasPermission(Permissions.AttendanceUpdateAll)]
    public async Task<IActionResult> Update(
        int employeeInformationId,
        DateOnly workDate,
        int shiftTemplateId,
        [FromBody] UpdateAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateAttendanceCommand(
            employeeInformationId, workDate, shiftTemplateId,
            request.CheckInAt, request.CheckOutAt,
            request.Status,
            request.Notes, request.OverrideReason), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    /// <summary>Sửa record theo Id — dùng cho record OT (không có ShiftTemplateId).</summary>
    [HttpPut("records/{id:int}")]
    [HasPermission(Permissions.AttendanceUpdateAll)]
    public async Task<IActionResult> UpdateById(
        int id,
        [FromBody] UpdateAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateAttendanceByIdCommand(
            id,
            request.CheckInAt, request.CheckOutAt,
            request.Status,
            request.Notes, request.OverrideReason), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Xuất "Bảng chấm công theo kỳ" .xlsx — layout chuẩn VN (4-row/NV dynamic theo ca, merged
    /// cells, freeze pane F8, chú thích ký hiệu cuối sheet). Row TC (Tăng ca) tự aggregate từ
    /// AttendanceRecord có OvertimeRequestId != NULL — read-only.
    /// </summary>
    [HttpGet("sheet/export")]
    [HasPermission(Permissions.AttendanceReadAll)]
    public async Task<IActionResult> ExportSheet(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] int? departmentId,
        CancellationToken cancellationToken)
    {
        var file = await _mediator.Send(
            new ExportAttendanceSheetQuery(fromDate, toDate, departmentId), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Bước 1/2 import: upload .xlsx → parse + resolve + build diff. Trả previewId cache 15p.
    /// KHÔNG ghi DB. FE render diff cho HR review trước khi Confirm.
    /// </summary>
    [HttpPost("sheet/import")]
    [HasPermission(Permissions.AttendanceUpdateAll)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> ImportSheetPreview(
        IFormFile file,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "EMPTY_FILE", message = "Vui lòng chọn file .xlsx." });

        using var stream = file.OpenReadStream();
        var result = await _mediator.Send(
            new PreviewAttendanceSheetImportCommand(stream, fromDate, toDate), cancellationToken);
        return Ok(result);
    }

    /// <summary>Bước 2/2 import: apply diff đã cache. Skip locked, transaction 1-shot.</summary>
    [HttpPost("sheet/import/{previewId}/confirm")]
    [HasPermission(Permissions.AttendanceUpdateAll)]
    public async Task<IActionResult> ImportSheetConfirm(
        string previewId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ConfirmAttendanceSheetImportCommand(previewId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Hủy phiên preview — xóa cache ngay. Idempotent (không lỗi nếu đã expire).</summary>
    [HttpDelete("sheet/import/{previewId}")]
    [HasPermission(Permissions.AttendanceUpdateAll)]
    public async Task<IActionResult> ImportSheetCancel(
        string previewId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new CancelAttendanceSheetImportCommand(previewId), cancellationToken);
        return NoContent();
    }
}
