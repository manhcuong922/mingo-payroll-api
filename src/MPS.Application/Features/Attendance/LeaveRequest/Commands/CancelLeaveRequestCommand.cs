using MediatR;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record CancelLeaveRequestCommand(
    int Id,
    string? CancelReason) : IRequest<bool>;
