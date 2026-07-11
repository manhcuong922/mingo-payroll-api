using MediatR;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

public sealed record CancelLateEarlyRequestCommand(int Id, string? CancelReason) : IRequest<bool>;
