using MediatR;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public sealed record DeleteShiftTemplateCommand(int Id) : IRequest<bool>;
