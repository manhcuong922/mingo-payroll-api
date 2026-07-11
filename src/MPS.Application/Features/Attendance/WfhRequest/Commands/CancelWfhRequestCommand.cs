using MediatR;

namespace MPS.Application.Features.Attendance.WfhRequest.Commands;

public sealed record CancelWfhRequestCommand(int Id, string? CancelReason) : IRequest<bool>;
