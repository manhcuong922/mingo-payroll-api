using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public sealed record GetOvertimeRequestByIdQuery(int Id) : IRequest<OvertimeRequestDto?>;
