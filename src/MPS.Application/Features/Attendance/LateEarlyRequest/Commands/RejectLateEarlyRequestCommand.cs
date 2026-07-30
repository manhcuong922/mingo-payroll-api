using MediatR;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

public sealed record RejectLateEarlyRequestCommand(int Id, string? ReviewNote) : IRequest<bool>;
