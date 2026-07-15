using MediatR;

namespace MPS.Application.Features.Attendance.WfhRequest.Commands;

public sealed record ApproveWfhRequestCommand(int Id, string? ReviewNote, string? OverrideReason) : IRequest<bool>;
