using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetEmployeeAttendanceDetailQueryHandler
    : IRequestHandler<GetEmployeeAttendanceDetailQuery, IReadOnlyList<AttendanceRecordDto>>
{
    private readonly IAttendanceRecordRepository _repository;

    public GetEmployeeAttendanceDetailQueryHandler(IAttendanceRecordRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> Handle(
        GetEmployeeAttendanceDetailQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetByEmployeeRangeAsync(
            request.EmployeeInformationId, request.FromDate, request.ToDate, cancellationToken);

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
            OvertimeProjectTitle: r.OvertimeRequest?.Project?.Title)).ToList();
    }
}
