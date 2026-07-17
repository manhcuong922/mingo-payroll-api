using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.API.Services;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.PublicHoliday.Commands;
using MPS.Application.Features.Attendance.PublicHoliday.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/public-holidays")]
[Authorize]
public class PublicHolidayController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicHolidayController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.PublicHolidayReadAll)]
    public async Task<IActionResult> GetByYear(
        [FromQuery] int year,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        if (year < 1900 || year > 9999)
            return BadRequest(new { error = "Year must be between 1900 and 9999." });
        var result = await _mediator.Send(new GetPublicHolidaysQuery(year, includeInactive), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.PublicHolidayCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePublicHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var endDate = request.HolidayEndDate ?? request.HolidayDate;
        var id = await _mediator.Send(new CreatePublicHolidayCommand(
            request.Name, request.HolidayDate, endDate,
            request.Description), cancellationToken);
        return Created($"/api/public-holidays/{id}", new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.PublicHolidayUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePublicHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var endDate = request.HolidayEndDate ?? request.HolidayDate;
        var updated = await _mediator.Send(new UpdatePublicHolidayCommand(
            id, request.Name, request.HolidayDate, endDate,
            request.Description, request.IsActive), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.PublicHolidayDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeletePublicHolidayCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpGet("import/template")]
    [HasPermission(Permissions.PublicHolidayCreateAll)]
    public IActionResult DownloadTemplate()
    {
        var bytes = PublicHolidayImportTemplateGenerator.Generate();
        return File(bytes, PublicHolidayImportTemplateGenerator.ExcelContentType, "public-holiday-import-template.xlsx");
    }

    [HttpPost("import/preview")]
    [HasPermission(Permissions.PublicHolidayCreateAll)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> PreviewImport(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Vui lòng chọn file Excel." });

        await using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new PreviewPublicHolidayImportCommand(stream), cancellationToken);
        return Ok(result);
    }

    [HttpPost("import/confirm")]
    [HasPermission(Permissions.PublicHolidayCreateAll)]
    public async Task<IActionResult> ConfirmImport(
        [FromBody] ConfirmPublicHolidayImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Rows is null || request.Rows.Count == 0)
            return BadRequest(new { error = "Không có dòng nào để import." });

        var result = await _mediator.Send(new ConfirmPublicHolidayImportCommand(request.Rows), cancellationToken);
        return Ok(result);
    }
}

public sealed record ConfirmPublicHolidayImportRequest(IReadOnlyList<PublicHolidayImportConfirmRow> Rows);
