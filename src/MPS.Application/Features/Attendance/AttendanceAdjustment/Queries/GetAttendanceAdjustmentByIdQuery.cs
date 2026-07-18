using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Queries;

public sealed record GetAttendanceAdjustmentByIdQuery(int Id) : IRequest<AttendanceAdjustmentDto?>;
