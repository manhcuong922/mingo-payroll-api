using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

public sealed record CreateLateEarlyRequestCommand(
    DateOnly WorkDate,
    LateEarlyKind Kind,
    short Minutes,
    string? Reason,
    OvertimeApproverRoute? ApproverRoute,
    string? OverrideReason) : IRequest<int>;
