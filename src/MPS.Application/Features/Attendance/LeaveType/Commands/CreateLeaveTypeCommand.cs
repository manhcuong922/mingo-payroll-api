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
    bool AllowEmployeeSelection,
    decimal? MaxDaysPerYear,
    decimal? MaxDaysPerMonth,
    decimal? MaxDaysPerWeek,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
    bool RequireDocument,
    bool DeductsBalance,
    int? MinNoticeDaysForShort,
    int? MinNoticeDaysForMedium,
    int? MinNoticeDaysForLong,
    bool AutoFallbackToUnpaid,
    bool AllowRetroactive,
    bool IsActive = true) : IRequest<int>, ILeaveTypeCommand;
