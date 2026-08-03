using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetMyAttendanceQueryHandler
    : IRequestHandler<GetMyAttendanceQuery, IReadOnlyList<AttendanceRecordDto>>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IDayTypeResolver _dayTypeResolver;

    public GetMyAttendanceQueryHandler(
        IAttendanceRecordRepository repository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IDayTypeResolver dayTypeResolver)
    {
        _repository = repository;
        _lookup = lookup;
        _currentUser = currentUser;
        _dayTypeResolver = dayTypeResolver;
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> Handle(
        GetMyAttendanceQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var items = await _repository.GetByEmployeeRangeAsync(
            employeeId, request.FromDate, request.ToDate, cancellationToken);

        // BR-23: 1 NV ⇒ resolve range 1 lần để phân biệt ngày phép có lương / không lương.
        var hasOnLeave = items.Any(r => r.Status == AttendanceStatus.OnLeave);
        var dayMap = hasOnLeave
            ? await _dayTypeResolver.ResolveRangeAsync(
                employeeId, request.FromDate, request.ToDate, cancellationToken)
            : null;

        return items.Select(r => new AttendanceRecordDto(
            r.Id,
            r.EmployeeInformationId,
            r.EmployeeInformation?.FullName ?? string.Empty,
            r.WorkDate,
            r.ShiftTemplateId,
            r.ShiftTemplate?.Name,
            r.CheckInAt,
            r.CheckOutAt,
            r.DayHours,
            r.NightHours,
            r.Status,
            r.Notes,
            r.OverrideReason,
            r.CreatedAt,
            r.UpdatedAt,
            OvertimeRequestId: r.OvertimeRequestId,
            OtStartTime: r.OvertimeRequest?.OtStartTime,
            OtEndTime: r.OvertimeRequest?.OtEndTime,
            OvertimeProjectTitle: r.OvertimeRequest?.Project?.Title,
            IsPaidLeave: r.Status == AttendanceStatus.OnLeave
                && dayMap != null
                && dayMap.TryGetValue(r.WorkDate, out var dt)
                && dt == DayType.PaidLeaveDay)).ToList();
    }
}
