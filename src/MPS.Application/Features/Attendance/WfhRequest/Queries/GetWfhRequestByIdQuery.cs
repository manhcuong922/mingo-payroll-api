using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Queries;

public sealed record GetWfhRequestByIdQuery(int Id) : IRequest<WfhRequestDto?>;
