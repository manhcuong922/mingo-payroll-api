using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MPS.API.RateLimiting;
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
        [FromQuery] string? keyword,
        [FromQuery] int? employeeInformationId,
        [FromQuery] ContractStatus? status,
        [FromQuery] ContractCategory? category,
        [FromQuery] ContractKind? kind,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetContractsQuery(keyword, employeeInformationId, status, category, kind, page, size),
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
            Kind: request.Kind,
            ContractCategory: request.ContractCategory,
            ContractSubtype: request.ContractSubtype,
            EmployeeInformationId: request.EmployeeInformationId,
            EmployerPartyId: request.EmployerPartyId,
            EmployerSignerUserId: request.EmployerSignerUserId,
            PositionId: request.PositionId,
            JobDescription: request.JobDescription,
            WorkLocation: request.WorkLocation,
            EffectiveFrom: request.EffectiveFrom,
            EffectiveTo: request.EffectiveTo,
            SalaryGradeId: request.SalaryGradeId,
            SalaryPayCycle: request.SalaryPayCycle,
            PromotionPolicy: request.PromotionPolicy,
            WorkingTimeJson: request.WorkingTimeJson,
            PpePolicy: request.PpePolicy,
            SocialInsuranceFlag: request.SocialInsuranceFlag,
            HealthInsuranceFlag: request.HealthInsuranceFlag,
            UnemploymentInsuranceFlag: request.UnemploymentInsuranceFlag,
            UnionFundExemptFlag: request.UnionFundExemptFlag,
            TaxPolicy: request.TaxPolicy,
            TrainingPolicy: request.TrainingPolicy,
            ConfidentialityClause: request.ConfidentialityClause,
            TemplateId: request.TemplateId,
            HasProbation: request.HasProbation,
            ProbationStartDate: request.ProbationStartDate,
            ProbationEndDate: request.ProbationEndDate,
            ProbationSalary: request.ProbationSalary,
            ProbationResultStatus: request.ProbationResultStatus,
            ParentContractId: request.ParentContractId,
            Allowances: (request.Allowances ?? Array.Empty<ContractAllowanceItemRequest>())
                .Select(a => new ContractAllowanceInput(a.AllowanceTypeId, a.Amount, a.Currency, a.Note))
                .ToList(),
            ForceAllowThirdDefinite: request.ForceAllowThirdDefinite);

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
            Id: id,
            Kind: request.Kind,
            ContractCategory: request.ContractCategory,
            ContractSubtype: request.ContractSubtype,
            EmployeeInformationId: request.EmployeeInformationId,
            EmployerPartyId: request.EmployerPartyId,
            EmployerSignerUserId: request.EmployerSignerUserId,
            PositionId: request.PositionId,
            JobDescription: request.JobDescription,
            WorkLocation: request.WorkLocation,
            EffectiveFrom: request.EffectiveFrom,
            EffectiveTo: request.EffectiveTo,
            SalaryGradeId: request.SalaryGradeId,
            SalaryPayCycle: request.SalaryPayCycle,
            PromotionPolicy: request.PromotionPolicy,
            WorkingTimeJson: request.WorkingTimeJson,
            PpePolicy: request.PpePolicy,
            SocialInsuranceFlag: request.SocialInsuranceFlag,
            HealthInsuranceFlag: request.HealthInsuranceFlag,
            UnemploymentInsuranceFlag: request.UnemploymentInsuranceFlag,
            UnionFundExemptFlag: request.UnionFundExemptFlag,
            TaxPolicy: request.TaxPolicy,
            TrainingPolicy: request.TrainingPolicy,
            ConfidentialityClause: request.ConfidentialityClause,
            TemplateId: request.TemplateId,
            HasProbation: request.HasProbation,
            ProbationStartDate: request.ProbationStartDate,
            ProbationEndDate: request.ProbationEndDate,
            ProbationSalary: request.ProbationSalary,
            ProbationResultStatus: request.ProbationResultStatus,
            ParentContractId: request.ParentContractId,
            Allowances: (request.Allowances ?? Array.Empty<ContractAllowanceItemRequest>())
                .Select(a => new ContractAllowanceInput(a.AllowanceTypeId, a.Amount, a.Currency, a.Note))
                .ToList(),
            ForceAllowThirdDefinite: request.ForceAllowThirdDefinite);

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

    /// <summary>
    /// [D12] — Lịch sử mức lương theo mốc hiệu lực (lần ký gốc + các phụ lục đổi lương đã kích hoạt).
    /// Đây là căn cứ để đối chiếu "kỳ lương tháng X trả theo mức nào".
    /// </summary>
    [HttpGet("{id:int}/salary-snapshots")]
    [HasAnyPermission(Permissions.ContractReadAll, Permissions.ContractReadSelf)]
    public async Task<IActionResult> GetSalarySnapshots(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContractSalarySnapshotsQuery(id), cancellationToken);
        return Ok(result);
    }

    // --- Files (download via presigned URL) ---

    [HttpGet("{id:int}/files")]
    [HasAnyPermission(Permissions.ContractFileReadAll, Permissions.ContractFileReadSelf)]
    public async Task<IActionResult> GetFiles(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetContractFilesQuery(id), cancellationToken);
        return Ok(result);
    }

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

    [HttpPost("{id:int}/activate")]
    [HasPermission(Permissions.ContractActivateAll)]
    [EnableRateLimiting(RateLimitPolicies.ContractUpload)]
    [RequestSizeLimit(25 * 1024 * 1024)]
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

    [HttpPost("{id:int}/files/replace")]
    [HasPermission(Permissions.ContractActivateAll)]
    [EnableRateLimiting(RateLimitPolicies.ContractUpload)]
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

    [HttpGet("{id:int}/generate-document")]
    [HasPermission(Permissions.ContractCreateAll)]
    public async Task<IActionResult> GenerateDocument(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new MPS.Application.Features.Contract.Queries.GenerateContractDocumentQuery(id),
            cancellationToken);

        return File(
            result.Content,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            result.FileName);
    }

    private static async Task<Stream> EnsureSeekableAsync(Stream source, long expectedLength, CancellationToken ct)
    {
        if (source.CanSeek) return source;
        var ms = new MemoryStream(capacity: (int)Math.Min(expectedLength, int.MaxValue));
        await source.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}
