using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetEmployeeAttendanceDetailQueryHandler
    : IRequestHandler<GetEmployeeAttendanceDetailQuery, IReadOnlyList<AttendanceRecordDto>>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IDayTypeResolver _dayTypeResolver;

    public GetEmployeeAttendanceDetailQueryHandler(
        IAttendanceRecordRepository repository,
        IDayTypeResolver dayTypeResolver)
    {
        _repository = repository;
        _dayTypeResolver = dayTypeResolver;
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> Handle(
        GetEmployeeAttendanceDetailQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetByEmployeeRangeAsync(
            request.EmployeeInformationId, request.FromDate, request.ToDate, cancellationToken);

        // BR-23: 1 NV ⇒ resolve range 1 lần, tra ngày phép có lương từ map.
        var hasOnLeave = items.Any(r => r.Status == AttendanceStatus.OnLeave);
        var dayMap = hasOnLeave
            ? await _dayTypeResolver.ResolveRangeAsync(
                request.EmployeeInformationId, request.FromDate, request.ToDate, cancellationToken)
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
