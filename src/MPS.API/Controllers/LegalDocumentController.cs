using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Compliance;
using MPS.Application.Features.Compliance.LegalDocument.Commands;
using MPS.Application.Features.Compliance.LegalDocument.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/compliance/legal-documents")]
public class LegalDocumentController : ControllerBase
{
    private readonly IMediator _mediator;

    public LegalDocumentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.LegalDocumentReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? documentType = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _mediator.Send(
            new GetAllLegalDocumentsQuery(pageNumber, pageSize, documentType, isActive, keyword), cancellationToken);
        return Ok(result);
    }

    [HttpGet("active")]
    [HasPermission(Permissions.LegalDocumentReadAll)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActiveLegalDocumentsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.LegalDocumentReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLegalDocumentByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:int}/compliance-versions")]
    [HasPermission(Permissions.LegalDocumentReadAll)]
    public async Task<IActionResult> GetComplianceVersions(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetVersionsByDocumentQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-version")]
    [HasPermission(Permissions.LegalDocumentReadAll)]
    public async Task<IActionResult> GetByVersion(
        [FromQuery] string entityType,
        [FromQuery] int versionId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLegalDocumentsByVersionQuery(entityType, versionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.LegalDocumentWriteAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateLegalDocumentCommand(
            request.Code,
            request.DocumentType,
            request.DocumentNumber,
            request.Title,
            request.IssuedBy,
            request.IssuedDate,
            request.EffectiveDate,
            request.ExpiryDate,
            request.Url,
            request.Note), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpDelete("{id:int}/deactivate")]
    [HasPermission(Permissions.LegalDocumentWriteAll)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeactivateLegalDocumentCommand(id), cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpPost("link")]
    [HasPermission(Permissions.LegalDocumentWriteAll)]
    public async Task<IActionResult> Link(
        [FromBody] LinkLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new LinkLegalDocumentCommand(
            request.ComplianceEntityType,
            request.ComplianceVersionId,
            request.LegalDocumentId), cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("unlink")]
    [HasPermission(Permissions.LegalDocumentWriteAll)]
    public async Task<IActionResult> Unlink(
        [FromBody] UnlinkLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UnlinkLegalDocumentCommand(
            request.ComplianceEntityType,
            request.ComplianceVersionId,
            request.LegalDocumentId), cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }
}
