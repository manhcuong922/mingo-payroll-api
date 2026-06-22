using MediatR;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record ApproveLeaveRequestCommand(int Id, string? ReviewNote) : IRequest<bool>;
