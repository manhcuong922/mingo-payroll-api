using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Queries;

public sealed record GetLateEarlyRequestByIdQuery(int Id) : IRequest<LateEarlyRequestDto?>;
