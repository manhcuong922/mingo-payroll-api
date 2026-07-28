using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Attendance;
using MPS.Application.Features.Attendance.FaceRegistration.Commands;
using MPS.Application.Features.Attendance.FaceRegistration.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/attendance/face-registrations")]
[Authorize]
public class FaceRegistrationController : ControllerBase
{
    private readonly IMediator _mediator;

    public FaceRegistrationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("my")]
    [HasPermission(Permissions.FaceRegistrationSubmitSelf)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMyFaceRegistrationsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [HasPermission(Permissions.FaceRegistrationReadAll)]
    public async Task<IActionResult> GetAll(
        [FromQuery] FaceRegistrationStatus? status,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllFaceRegistrationsQuery(status), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.FaceRegistrationSubmitSelf)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitFaceRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SubmitFaceRegistrationCommand(request.PhotoBase64, request.EmbeddingJson),
            cancellationToken);
        return Created($"/api/attendance/face-registrations/{result.Id}", result);
    }

    [HttpPut("{id:guid}/approve")]
    [HasPermission(Permissions.FaceRegistrationApproveAll)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApproveFaceRegistrationCommand(id), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/reject")]
    [HasPermission(Permissions.FaceRegistrationApproveAll)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectFaceRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RejectFaceRegistrationCommand(id, request.Reason),
            cancellationToken);
        return Ok(result);
    }

    // ===== AI Service endpoints =====

    /// <summary>
    /// AI service gọi mỗi giờ để refresh embedding cache.
    /// Chỉ cho phép role AiService.
    /// </summary>
    [HttpGet("embeddings")]
    [Authorize(Policy = "ServiceAccount")]
    public async Task<IActionResult> GetApprovedEmbeddings(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetApprovedEmbeddingsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// AI service gọi sau mỗi lần nhận diện khuôn mặt thành công để chấm công tự động.
    /// Chỉ cho phép role AiService.
    /// </summary>
    [HttpPost("checkin")]
    [Authorize(Policy = "ServiceAccount")]
    public async Task<IActionResult> FaceCheckin(
        [FromBody] FaceCheckinRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new FaceCheckinCommand(
                request.EmployeeId,
                request.CheckinTime,
                request.Confidence,
                request.CameraId,
                request.Source),
            cancellationToken);
        return Ok(result);
    }
}
