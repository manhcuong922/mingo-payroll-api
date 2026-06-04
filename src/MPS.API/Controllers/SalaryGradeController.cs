using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.SalaryGrade;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Application.Features.SalaryGrade.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/salary-grades")]
public class SalaryGradeController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalaryGradeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(Permissions.SalaryGradeReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? positionId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SalaryGradeGetAllQuery(positionId, isActive), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.SalaryGradeReadAll)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SalaryGradeGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.SalaryGradeCreateAll)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSalaryGradeRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new SalaryGradeCreateCommand(
            request.Code,
            request.Name,
            request.PositionId,
            request.Level,
            request.BaseSalary,
            request.Currency,
            request.Description,
            request.IsActive), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.SalaryGradeUpdateAll)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateSalaryGradeRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new SalaryGradeUpdateCommand(
            id,
            request.Code,
            request.Name,
            request.PositionId,
            request.Level,
            request.BaseSalary,
            request.Currency,
            request.Description,
            request.IsActive), cancellationToken);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.SalaryGradeDeleteAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new SalaryGradeDeleteCommand(id), cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
