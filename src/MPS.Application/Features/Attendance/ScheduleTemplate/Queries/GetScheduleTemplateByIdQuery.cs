using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Queries;

public sealed record GetScheduleTemplateByIdQuery(int Id) : IRequest<ScheduleTemplateDto?>;
