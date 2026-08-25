using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Payroll;
using MPS.Application.Features.Payroll.Commands;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

/// <summary>
/// Bậc HỖ TRỢ THEO TIẾN ĐỘ của hợp đồng khoán việc: tổng giờ làm trong tháng → số tiền hỗ trợ
/// (quyết định PO 18/08/2026: từ 150 giờ = 200.000đ, từ 200 giờ = 500.000đ, không cộng dồn).
///
/// <para>Sửa bậc ⇒ mọi kỳ lương Draft bị đánh cờ tính lại; kỳ đã chốt giữ snapshot riêng nên không
/// đổi số.</para>
/// </summary>
[ApiController]
[Route("api/payroll/piece-work-support-bands")]
public class PieceWorkSupportBandController : ControllerBase
{
    private readonly IMediator _mediator;
    public PieceWorkSupportBandController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(Permissions.PieceWorkSupportBandReadAll)]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPieceWorkSupportBandsQuery(isActive), ct));

    [HttpPost]
    [HasPermission(Permissions.PieceWorkSupportBandUpdateAll)]
    public async Task<IActionResult> Create([FromBody] CreatePieceWorkSupportBandRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreatePieceWorkSupportBandCommand(
            request.MinHours, request.MaxHours, request.Amount, request.Label), ct);
        return Ok(new { id });
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.PieceWorkSupportBandUpdateAll)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePieceWorkSupportBandRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdatePieceWorkSupportBandCommand(
            id, request.MinHours, request.MaxHours, request.Amount, request.Label, request.IsActive), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.PieceWorkSupportBandUpdateAll)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _mediator.Send(new DeletePieceWorkSupportBandCommand(id), ct);
        return NoContent();
    }
}
