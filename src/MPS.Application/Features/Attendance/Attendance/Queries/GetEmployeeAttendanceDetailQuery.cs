using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public sealed record GetEmployeeAttendanceDetailQuery(
    int EmployeeInformationId,
    DateOnly FromDate,
    DateOnly ToDate) : IRequest<IReadOnlyList<AttendanceRecordDto>>;
