using MediatR;
using MPS.Application.Features.Attendance.LeaveType.Validators;

namespace MPS.Application.Features.Attendance.LeaveType.Commands;

public sealed record UpdateLeaveTypeCommand(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    decimal? PaidRate,
    decimal? MaxDaysPerRequest,
    bool AllowEmployeeSelection,
    decimal? MaxDaysPerYear,
    decimal? MaxDaysPerMonth,
    decimal? MaxDaysPerWeek,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
    bool RequireDocument,
    bool DeductsBalance,
    bool IsActive) : IRequest<bool>, ILeaveTypeCommand;
