using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public sealed record GetLeaveRequestByIdQuery(int Id) : IRequest<LeaveRequestDto?>;
