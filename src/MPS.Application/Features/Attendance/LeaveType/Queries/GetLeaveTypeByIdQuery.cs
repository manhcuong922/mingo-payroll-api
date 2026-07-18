using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.LeaveType.Queries;

public sealed record GetLeaveTypeByIdQuery(int Id) : IRequest<LeaveTypeDto?>;
