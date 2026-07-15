using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Payroll;
using MPS.Application.Features.Payroll.Commands;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

/// <summary>Tham số chính sách công ty (vd đơn giá cơm/công) — công thức tham chiếu qua param(Key).</summary>
[ApiController]
[Route("api/payroll/parameters")]
public class PayrollParameterController : ControllerBase
{
    private readonly IMediator _mediator;
    public PayrollParameterController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(Permissions.PayrollParameterReadAll)]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollParametersQuery(isActive), ct));

    [HttpPost]
    [HasPermission(Permissions.PayrollParameterUpdateAll)]
    public async Task<IActionResult> Create([FromBody] CreatePayrollParameterRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreatePayrollParameterCommand(
            request.Key, request.Name, request.Value, request.Description), ct);
        return Ok(new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.PayrollParameterUpdateAll)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePayrollParameterRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdatePayrollParameterCommand(
            id, request.Name, request.Value, request.Description, request.IsActive), ct);
        return NoContent();
    }
}
