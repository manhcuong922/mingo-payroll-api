using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Queries;

public sealed record GetAttendanceSettingQuery : IRequest<AttendanceSettingDto>;
