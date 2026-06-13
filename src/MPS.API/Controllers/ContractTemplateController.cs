using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.ContractTemplate;
using MPS.Application.Features.ContractTemplate.Commands;
using MPS.Application.Features.ContractTemplate.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/contract-templates")]
public class ContractTemplateController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractTemplateController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.ContractTemplateReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] ContractCategory? category,
        [FromQuery] ContractSubtype? subtype,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ContractTemplateGetAllQuery(category, subtype, isActive), cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [HasPermission(Permissions.ContractTemplateReadAll)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] ContractCategory? category,
        [FromQuery] ContractSubtype? subtype,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _mediator.Send(
            new ContractTemplateGetPagedQuery(category, subtype, isActive, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.ContractTemplateReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ContractTemplateGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.ContractTemplateCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContractTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new ContractTemplateCreateCommand(
            request.Name,
            request.ContractCategory,
            request.ContractSubtype,
            request.IsActive), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ContractTemplateUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateContractTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new ContractTemplateUpdateCommand(
            id,
            request.Name,
            request.ContractCategory,
            request.ContractSubtype,
            request.IsActive), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ContractTemplateDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new ContractTemplateDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpGet("{id:int}/download")]
    [HasPermission(Permissions.ContractTemplateReadAll)]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        ContractTemplateDownloadResult result;
        try
        {
            result = await _mediator.Send(new GetContractTemplateDownloadQuery(id), cancellationToken);
        }
        catch (DomainValidationException ex) when (
            ex.Message.StartsWith("TEMPLATE_NOT_FOUND") ||
            ex.Message.StartsWith("TEMPLATE_FILE_NOT_FOUND"))
        {
            return NotFound(new { error = ex.Message });
        }

        return File(result.DocxStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.FileName);
    }

    [HttpPost("{id:int}/upload")]
    [HasPermission(Permissions.ContractTemplateCreateAll)]
    [RequestSizeLimit(5_242_880)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile(int id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "FILE_REQUIRED" });

        try
        {
            var fileId = await _mediator.Send(new UploadContractTemplateCommand(
                TemplateId: id,
                FileStream: file.OpenReadStream(),
                FileName: file.FileName,
                FileSizeBytes: file.Length), cancellationToken);

            return Ok(new { fileId });
        }
        catch (DomainValidationException ex) when (ex.Message.StartsWith("TEMPLATE_NOT_FOUND"))
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
