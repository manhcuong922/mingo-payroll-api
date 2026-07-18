using MediatR;

namespace MPS.Application.Features.Attendance.LeaveType.Commands;

public sealed record DeleteLeaveTypeCommand(int Id) : IRequest<bool>;
