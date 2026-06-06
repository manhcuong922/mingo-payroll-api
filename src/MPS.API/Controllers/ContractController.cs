using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MPS.API.Authorization;
using MPS.API.Requests.Contract;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize]
public class ContractController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasAnyPermission(Permissions.ContractReadAll, Permissions.ContractReadSelf)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeId,
        [FromQuery] ContractStatus? status,
        [FromQuery] ContractCategory? category,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetContractsQuery(employeeId, status, category, page, size),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasAnyPermission(Permissions.ContractReadAll, Permissions.ContractReadSelf)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContractByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.ContractCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContractRequest request,
        CancellationToken cancellationToken)
    {
        var cmd = new CreateContractCommand(
            request.ContractNo,
            request.ContractCategory,
            request.ContractSubtype,
            request.Form,
            request.EmployeeId,
            request.EmployerPartyId,
            request.EmployerSignerUserId,
            request.PositionId,
            request.JobDescription,
            request.WorkLocation,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.SalaryGradeId,
            request.SalaryFormType,
            request.SalaryPayCycle,
            request.PromotionPolicy,
            request.WorkingTimeJson,
            request.PpePolicy,
            request.SocialInsuranceFlag,
            request.HealthInsuranceFlag,
            request.UnemploymentInsuranceFlag,
            request.TrainingPolicy,
            request.ConfidentialityClause,
            request.ContractText,
            request.TemplateId,
            request.HasProbation,
            request.ProbationStartDate,
            request.ProbationEndDate,
            request.ProbationSalary,
            request.ProbationResultStatus,
            request.ParentContractId,
            (request.Allowances ?? Array.Empty<ContractAllowanceItemRequest>())
                .Select(a => new ContractAllowanceInput(a.AllowanceTypeId, a.Amount, a.Currency, a.Note))
                .ToList(),
            request.ForceAllowThirdDefinite);

        var id = await _mediator.Send(cmd, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ContractUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateContractRequest request,
        CancellationToken cancellationToken)
    {
        var cmd = new UpdateContractCommand(
            id,
            request.ContractNo,
            request.ContractCategory,
            request.ContractSubtype,
            request.Form,
            request.EmployeeId,
            request.EmployerPartyId,
            request.EmployerSignerUserId,
            request.PositionId,
            request.JobDescription,
            request.WorkLocation,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.SalaryGradeId,
            request.SalaryFormType,
            request.SalaryPayCycle,
            request.PromotionPolicy,
            request.WorkingTimeJson,
            request.PpePolicy,
            request.SocialInsuranceFlag,
            request.HealthInsuranceFlag,
            request.UnemploymentInsuranceFlag,
            request.TrainingPolicy,
            request.ConfidentialityClause,
            request.ContractText,
            request.TemplateId,
            request.HasProbation,
            request.ProbationStartDate,
            request.ProbationEndDate,
            request.ProbationSalary,
            request.ProbationResultStatus,
            request.ParentContractId,
            (request.Allowances ?? Array.Empty<ContractAllowanceItemRequest>())
                .Select(a => new ContractAllowanceInput(a.AllowanceTypeId, a.Amount, a.Currency, a.Note))
                .ToList(),
            request.ForceAllowThirdDefinite);

        var updated = await _mediator.Send(cmd, cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ContractDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteContractCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.ContractCancelAll)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new CancelContractCommand(id), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    // --- Allowance sub-resource ---

    [HttpPost("{id:int}/allowances")]
    [HasPermission(Permissions.ContractUpdateAll)]
    public async Task<IActionResult> AddAllowance(
        int id,
        [FromBody] AddContractAllowanceRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new AddContractAllowanceCommand(
            id, request.AllowanceTypeId, request.Amount, request.Currency, request.Note), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:int}/allowances/{allowanceTypeId:int}")]
    [HasPermission(Permissions.ContractUpdateAll)]
    public async Task<IActionResult> UpdateAllowance(
        int id,
        int allowanceTypeId,
        [FromBody] UpdateContractAllowanceRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new UpdateContractAllowanceCommand(
            id, allowanceTypeId, request.Amount, request.Currency, request.Note), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}/allowances/{allowanceTypeId:int}")]
    [HasPermission(Permissions.ContractUpdateAll)]
    public async Task<IActionResult> RemoveAllowance(
        int id,
        int allowanceTypeId,
        CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new RemoveContractAllowanceCommand(id, allowanceTypeId), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    // --- Files (download via presigned URL) ---

    /// <summary>
    /// GET /api/contracts/{id}/files — list all PDF versions của HĐ (TargetType=Contract),
    /// kèm presigned download URL TTL 15 phút. Self-scope check ở handler.
    /// </summary>
    [HttpGet("{id:int}/files")]
    [HasAnyPermission(Permissions.ContractFileReadAll, Permissions.ContractFileReadSelf)]
    public async Task<IActionResult> GetFiles(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContractFilesQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/contracts/{id}/termination/files — list all termination notice PDF versions
    /// (TargetType=Termination). Có thể trống nếu HĐ chưa terminate.
    /// </summary>
    [HttpGet("{id:int}/termination/files")]
    [HasAnyPermission(Permissions.ContractFileReadAll, Permissions.ContractFileReadSelf)]
    public async Task<IActionResult> GetTerminationFiles(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new MPS.Application.Features.ContractTermination.Queries.GetTerminationFilesQuery(id),
            cancellationToken);
        return Ok(result);
    }

    // --- Phase 6: Activation + file replace ---

    /// <summary>
    /// Upload signed PDF and transition the contract DRAFT → ACTIVE. Freezes the salary snapshot.
    /// Rate-limited (5/min/user) to avoid abuse since each call writes to MinIO.
    /// </summary>
    [HttpPost("{id:int}/activate")]
    [HasPermission(Permissions.ContractActivateAll)]
    [EnableRateLimiting("contract-upload")]
    [RequestSizeLimit(25 * 1024 * 1024)] // 25 MB hard cap at Kestrel; handler enforces 20 MB business limit.
    public async Task<IActionResult> Activate(
        int id,
        [FromForm] ActivateContractRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length <= 0)
            return BadRequest(new { errors = new[] { "File is required." } });

        await using var stream = request.File.OpenReadStream();
        var seekableStream = await EnsureSeekableAsync(stream, request.File.Length, cancellationToken);

        var contractFileId = await _mediator.Send(new ActivateContractCommand(
            ContractId: id,
            PdfStream: seekableStream,
            FileSizeBytes: request.File.Length,
            FileName: request.File.FileName,
            ContentType: request.File.ContentType ?? string.Empty,
            SignedAt: request.SignedAt,
            SignedPlace: request.SignedPlace,
            SignedBySource: request.SignedBySource), cancellationToken);

        return Ok(new { contractFileId });
    }

    /// <summary>
    /// Replace the current signed PDF after activation. Old row keeps history (IsCurrent=false),
    /// new row is Version=N+1, IsCurrent=true. Rejected if contract is archived.
    /// </summary>
    [HttpPost("{id:int}/files/replace")]
    [HasPermission(Permissions.ContractActivateAll)]
    [EnableRateLimiting("contract-upload")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> ReplaceFile(
        int id,
        [FromForm] ReplaceContractFileRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length <= 0)
            return BadRequest(new { errors = new[] { "File is required." } });

        await using var stream = request.File.OpenReadStream();
        var seekableStream = await EnsureSeekableAsync(stream, request.File.Length, cancellationToken);

        var contractFileId = await _mediator.Send(new ReplaceContractFileCommand(
            ContractId: id,
            PdfStream: seekableStream,
            FileSizeBytes: request.File.Length,
            FileName: request.File.FileName,
            ContentType: request.File.ContentType ?? string.Empty,
            SignedBySource: request.SignedBySource), cancellationToken);

        return Ok(new { contractFileId });
    }

    /// <summary>
    /// Form-file streams from Kestrel/IIS are usually non-seekable; PdfStreamValidator and
    /// the SHA-256 hashing in the storage layer both need to seek/read+rewind. Buffer into
    /// a small MemoryStream when needed (file is already bounded by RequestSizeLimit).
    /// </summary>
    private static async Task<Stream> EnsureSeekableAsync(Stream source, long expectedLength, CancellationToken ct)
    {
        if (source.CanSeek) return source;
        var ms = new MemoryStream(capacity: (int)Math.Min(expectedLength, int.MaxValue));
        await source.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}
