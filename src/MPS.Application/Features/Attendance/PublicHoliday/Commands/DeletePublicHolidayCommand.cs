using MediatR;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public sealed record DeletePublicHolidayCommand(int Id) : IRequest<bool>;