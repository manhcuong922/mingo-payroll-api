using MediatR;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record RejectLeaveRequestCommand(int Id, string? ReviewNote) : IRequest<bool>;
