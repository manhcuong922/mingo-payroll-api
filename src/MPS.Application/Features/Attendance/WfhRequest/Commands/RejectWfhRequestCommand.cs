using MediatR;

namespace MPS.Application.Features.Attendance.WfhRequest.Commands;

public sealed record RejectWfhRequestCommand(int Id, string? ReviewNote) : IRequest<bool>;
