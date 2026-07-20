using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.API.Services;
using MPS.Application.Features.Attendance.ScheduleTemplate.Commands;
using MPS.Application.Features.Attendance.ScheduleTemplate.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/schedule-templates")]
[Authorize]
public class ScheduleTemplateController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IShiftTemplateRepository _shiftTemplateRepository;

    public ScheduleTemplateController(
        IMediator mediator,
        IShiftTemplateRepository shiftTemplateRepository)
    {
        _mediator = mediator;
        _shiftTemplateRepository = shiftTemplateRepository;
    }

    [HttpGet]
    [HasPermission(Permissions.EmployeeShiftReadAll)]
    public async Task<IActionResult> GetList(
        [FromQuery] bool? isActive,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetScheduleTemplatesQuery(isActive, keyword, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.EmployeeShiftReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetScheduleTemplateByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateScheduleTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateScheduleTemplateCommand(
            request.Name,
            ResolveShiftTemplateIds(request.ShiftTemplateId, request.ShiftTemplateIds),
            request.EffectiveFrom,
            request.EffectiveTo,
            request.RepeatUnit,
            request.RepeatEvery,
            request.DaysOfWeek,
            request.DaysOfMonth,
            request.EmployeeInformationIds,
            request.Notes,
            request.AcknowledgeWeeklyOvertimeWarnings,
            request.ApplyTarget,
            request.DepartmentIds,
            request.IncludeRestDaysAndHolidays), cancellationToken);
        return Created($"/api/schedule-templates/{result.ScheduleTemplateId}", result);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateScheduleTemplateRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdateScheduleTemplateCommand(
            id,
            request.Name,
            ResolveShiftTemplateIds(request.ShiftTemplateId, request.ShiftTemplateIds),
            request.EffectiveFrom,
            request.EffectiveTo,
            request.RepeatUnit,
            request.RepeatEvery,
            request.DaysOfWeek,
            request.DaysOfMonth,
            request.EmployeeInformationIds,
            request.IsActive,
            request.Notes,
            request.ApplyTarget,
            request.DepartmentIds,
            request.IncludeRestDaysAndHolidays,
            request.AcknowledgeWeeklyOvertimeWarnings), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reapply")]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    public async Task<IActionResult> Reapply(
        int id,
        [FromQuery] bool acknowledgeWeeklyOvertimeWarnings = false,
        [FromQuery] bool includeRestDaysAndHolidays = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ReapplyScheduleTemplateCommand(id, acknowledgeWeeklyOvertimeWarnings, includeRestDaysAndHolidays),
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.EmployeeShiftDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteScheduleTemplateCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("import-monthly/template")]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    public async Task<IActionResult> DownloadMonthlyImportTemplate(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] MPS.Domain.Enums.Attendance.ScheduleApplyTarget applyTarget
            = MPS.Domain.Enums.Attendance.ScheduleApplyTarget.Employees,
        CancellationToken cancellationToken = default)
    {
        var targetMonth = ResolveTemplateMonth(year, month);
        if (targetMonth is null)
            return BadRequest(new { error = "Query year/month không hợp lệ." });

        var activeShifts = await _shiftTemplateRepository.GetPagedAsync(
            isActive: true,
            page: 1,
            size: 1000,
            cancellationToken);
        var bytes = MonthlyScheduleImportTemplateGenerator.Generate(
            targetMonth.Value.Year,
            targetMonth.Value.Month,
            activeShifts.Items,
            applyTarget);

        var modeSuffix = applyTarget == MPS.Domain.Enums.Attendance.ScheduleApplyTarget.Departments
            ? "phong-ban"
            : "nhan-vien";
        return File(
            bytes,
            MonthlyScheduleImportTemplateGenerator.ExcelContentType,
            $"phan-ca-thang-{targetMonth.Value.Month:00}-{targetMonth.Value.Year}-{modeSuffix}-mau.xlsx");
    }

    [HttpPost("import-monthly/preview")]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> PreviewMonthlyImport(
        IFormFile file,
        [FromForm(Name = "applyTarget")] MPS.Domain.Enums.Attendance.ScheduleApplyTarget applyTarget
            = MPS.Domain.Enums.Attendance.ScheduleApplyTarget.Employees,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        // Copy sang MemoryStream rồi đóng IFormFile gốc — tránh lifetime issue khi
        // stream gốc bị ASP.NET dispose trước khi handler đọc xong.
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var result = await _mediator.Send(
            new PreviewMonthlyScheduleImportCommand(buffer, applyTarget),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("import-monthly/confirm")]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    public async Task<IActionResult> ConfirmMonthlyImport(
        [FromBody] ConfirmMonthlyScheduleImportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ConfirmMonthlyScheduleImportCommand(
                request.PreviewToken,
                request.AcknowledgeWeeklyOvertimeWarnings),
            cancellationToken);
        return Ok(result);
    }

    private static DateOnly? ResolveTemplateMonth(int? year, int? month)
    {
        var nowVietnam = DateTime.UtcNow.AddHours(7);
        var resolvedYear = year ?? nowVietnam.Year;
        var resolvedMonth = month ?? nowVietnam.Month;

        if (resolvedYear is < 2000 or > 2100)
            return null;
        if (resolvedMonth is < 1 or > 12)
            return null;

        return new DateOnly(resolvedYear, resolvedMonth, 1);
    }

    private static IReadOnlyList<int> ResolveShiftTemplateIds(
        int shiftTemplateId,
        IReadOnlyList<int>? shiftTemplateIds)
    {
        var ids = shiftTemplateIds is { Count: > 0 }
            ? shiftTemplateIds
            : new[] { shiftTemplateId };

        return ids
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }
}
