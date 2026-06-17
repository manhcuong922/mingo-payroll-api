using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Queries;

public sealed record GetShiftTemplateByIdQuery(int Id) : IRequest<ShiftTemplateDto?>;
