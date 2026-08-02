using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetDailyAttendanceQueryHandler
    : IRequestHandler<GetDailyAttendanceQuery, IReadOnlyList<AttendanceRecordDto>>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IDayTypeResolver _dayTypeResolver;

    public GetDailyAttendanceQueryHandler(
        IAttendanceRecordRepository repository,
        IDayTypeResolver dayTypeResolver)
    {
        _repository = repository;
        _dayTypeResolver = dayTypeResolver;
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> Handle(
        GetDailyAttendanceQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetByDateAsync(request.WorkDate, cancellationToken);
        var result = new List<AttendanceRecordDto>(items.Count);
        foreach (var r in items)
        {
            // BR-23: chỉ resolve DayType cho record OnLeave — ngày phép có lương ⇒ IsPaidLeave = true.
            // Sequential vì DbContext không thread-safe; chỉ chạy khi có OnLeave nên số call thường nhỏ.
            var isPaidLeave = r.Status == AttendanceStatus.OnLeave
                && await _dayTypeResolver.ResolveAsync(r.EmployeeInformationId, r.WorkDate, cancellationToken)
                    == DayType.PaidLeaveDay;
            result.Add(new AttendanceRecordDto(
                r.Id,
                r.EmployeeInformationId,
                r.EmployeeInformation.FullName,
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
                IsPaidLeave: isPaidLeave));
        }
        return result;
    }
}
