using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public sealed record GetDailyAttendanceQuery(DateOnly WorkDate) : IRequest<IReadOnlyList<AttendanceRecordDto>>;
