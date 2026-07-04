using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.ScheduleTemplate.Commands;
using MPS.Application.Features.Attendance.ScheduleTemplate.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/schedule-templates")]
[Authorize]
public class ScheduleTemplateController : ControllerBase
{
    private readonly IMediator _mediator;

    public ScheduleTemplateController(IMediator mediator)
    {
        _mediator = mediator;
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
            request.ShiftTemplateId,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.RepeatUnit,
            request.RepeatEvery,
            request.DaysOfWeek,
            request.DaysOfMonth,
            request.EmployeeInformationIds,
            request.Notes,
            request.AcknowledgeWeeklyOvertimeWarnings), cancellationToken);
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
            request.ShiftTemplateId,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.RepeatUnit,
            request.RepeatEvery,
            request.DaysOfWeek,
            request.DaysOfMonth,
            request.EmployeeInformationIds,
            request.IsActive,
            request.Notes), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reapply")]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    public async Task<IActionResult> Reapply(
        int id,
        [FromQuery] bool acknowledgeWeeklyOvertimeWarnings = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ReapplyScheduleTemplateCommand(id, acknowledgeWeeklyOvertimeWarnings),
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
    public IActionResult DownloadMonthlyImportTemplate()
    {
        const string resourceName = "MPS.API.Resources.sample-monthly-shift-import.xlsx";
        var assembly = typeof(ScheduleTemplateController).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return NotFound(new { error = "Template resource not found." });

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return File(
            buffer.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "phan-ca-thang-mau.xlsx");
    }

    [HttpPost("import-monthly/preview")]
    [HasPermission(Permissions.EmployeeShiftCreateAll)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> PreviewMonthlyImport(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        // Copy sang MemoryStream rồi đóng IFormFile gốc — tránh lifetime issue khi
        // stream gốc bị ASP.NET dispose trước khi handler đọc xong.
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var result = await _mediator.Send(new PreviewMonthlyScheduleImportCommand(buffer), cancellationToken);
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
}
