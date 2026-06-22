using MediatR;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public sealed record DeleteScheduleTemplateCommand(int Id) : IRequest<bool>;
