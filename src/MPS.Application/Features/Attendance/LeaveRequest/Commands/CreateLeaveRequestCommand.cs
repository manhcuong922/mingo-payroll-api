using MediatR;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record CreateLeaveRequestCommand(
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason,
    string? SupportingDocumentUrl) : IRequest<int>;
