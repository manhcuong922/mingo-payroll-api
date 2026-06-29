using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.CompanyPolicy;
using MPS.Application.Features.CompanyPolicy.Commands;
using MPS.Application.Features.CompanyPolicy.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/company-policies")]
[Authorize]
public class CompanyPolicyController : ControllerBase
{
    private const long MaxUploadBytes = 25L * 1024L * 1024L;

    private readonly IMediator _mediator;

    public CompanyPolicyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.CompanyPolicyReadAll)]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetCompanyPoliciesQuery(isActive, keyword, page, size), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.CompanyPolicyReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCompanyPolicyByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}/files/{fileId:int}/content")]
    [HasPermission(Permissions.CompanyPolicyReadAll)]
    public async Task<IActionResult> DownloadFile(
        int id, int fileId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new DownloadCompanyPolicyFileQuery(id, fileId), cancellationToken);

        // Use inline so PDFs render in the browser viewer; FE forces download
        // via blob if user explicitly clicks "Tải về".
        var cd = new System.Net.Mime.ContentDisposition
        {
            FileName = result.FileName,
            Inline = true,
        };
        Response.Headers.Append("Content-Disposition", cd.ToString());
        return File(result.Content, result.ContentType, enableRangeProcessing: true);
    }

    [HttpPost]
    [HasPermission(Permissions.CompanyPolicyCreateAll)]
    [RequestSizeLimit(MaxUploadBytes + 1_048_576)] // +1MB for multipart overhead
    public async Task<IActionResult> Create(
        [FromForm] CreateCompanyPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest(new { error = "File is required." });

        using var buffer = new MemoryStream();
        await request.File.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var id = await _mediator.Send(new CreateCompanyPolicyCommand(
            Title: request.Title,
            Description: request.Description,
            FileStream: buffer,
            FileName: request.File.FileName,
            FileSizeBytes: request.File.Length,
            Notes: request.Notes), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.CompanyPolicyUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateCompanyPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new UpdateCompanyPolicyMetadataCommand(
            id, request.Title, request.Description, request.IsActive), cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/versions")]
    [HasPermission(Permissions.CompanyPolicyUpdateAll)]
    [RequestSizeLimit(MaxUploadBytes + 1_048_576)]
    public async Task<IActionResult> UploadVersion(
        int id,
        [FromForm] UploadCompanyPolicyVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest(new { error = "File is required." });

        using var buffer = new MemoryStream();
        await request.File.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var fileId = await _mediator.Send(new UploadCompanyPolicyVersionCommand(
            PolicyId: id,
            FileStream: buffer,
            FileName: request.File.FileName,
            FileSizeBytes: request.File.Length,
            Notes: request.Notes), cancellationToken);

        return Ok(new { fileId });
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.CompanyPolicyDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteCompanyPolicyCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
