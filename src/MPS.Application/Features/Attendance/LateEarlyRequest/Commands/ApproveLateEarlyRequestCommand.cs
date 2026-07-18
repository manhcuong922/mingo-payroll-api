using MediatR;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

public sealed record ApproveLateEarlyRequestCommand(
    int Id,
    string? ReviewNote,
    bool DeductFromLeaveBalance,
    string? OverrideReason) : IRequest<ApproveLateEarlyRequestResult>;

public sealed record ApproveLateEarlyRequestResult(
    bool Approved,
    bool MustDeductBecauseQuotaExceeded,
    string? Warning);
