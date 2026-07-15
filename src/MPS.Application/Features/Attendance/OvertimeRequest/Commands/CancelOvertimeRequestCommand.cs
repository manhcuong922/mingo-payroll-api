using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

public sealed record CancelOvertimeRequestCommand(int Id) : IRequest<bool>;
