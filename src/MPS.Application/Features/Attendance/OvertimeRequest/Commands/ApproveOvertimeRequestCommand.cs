using MediatR;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

public sealed record ApproveOvertimeRequestCommand(int Id, string? ReviewNote) : IRequest<bool>;
