using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Payroll;
using MPS.Application.Features.Payroll.Commands;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

/// <summary>Bảng map điểm đánh giá tháng → hệ số nhân lương gốc (cột Trợ cấp lương chính).</summary>
[ApiController]
[Route("api/payroll/productivity-coefficients")]
public class ProductivityCoefficientController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProductivityCoefficientController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(Permissions.ProductivityCoefficientReadAll)]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProductivityCoefficientsQuery(isActive), ct));

    [HttpPost]
    [HasPermission(Permissions.ProductivityCoefficientUpdateAll)]
    public async Task<IActionResult> Create([FromBody] CreateProductivityCoefficientRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateProductivityCoefficientCommand(
            request.MinScore, request.MaxScore, request.Coefficient, request.Label), ct);
        return Ok(new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ProductivityCoefficientUpdateAll)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductivityCoefficientRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateProductivityCoefficientCommand(
            id, request.MinScore, request.MaxScore, request.Coefficient, request.Label, request.IsActive), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ProductivityCoefficientUpdateAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteProductivityCoefficientCommand(id), ct);
        return NoContent();
    }
}
