using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Commands;

public sealed record CreateWfhRequestCommand(
    DateOnly StartDate,
    DateOnly EndDate,
    string WorkPlan,
    string? Reason,
    OvertimeApproverRoute? ApproverRoute,
    string? OverrideReason) : IRequest<int>;
