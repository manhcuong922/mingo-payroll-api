using MediatR;
using MPS.Application.Features.Attendance.LeaveType.Validators;

namespace MPS.Application.Features.Attendance.LeaveType.Commands;

public sealed record CreateLeaveTypeCommand(
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    decimal? PaidRate,
    decimal? MaxDaysPerRequest,
    bool RequireDocument,
    bool DeductsBalance) : IRequest<int>, ILeaveTypeCommand;
