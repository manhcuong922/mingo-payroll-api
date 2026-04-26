using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.Application.Features.Example.Commands;
using MPS.Application.Features.Example.Queries;

namespace MPS.API.Controllers;

/// <summary>
/// Ví dụ Controller - điểm vào API, chuyển tiếp request đến Application layer.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ExampleController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExampleController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExampleGetByIdQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExampleRequest request, CancellationToken cancellationToken)
    {
        var command = new ExampleCreateCommand(request.Name);
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }
}

/// <summary>
/// Request body cho tạo Example.
/// </summary>
public class CreateExampleRequest
{
    public string Name { get; set; } = string.Empty;
}
