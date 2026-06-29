using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetDailyAttendanceQueryHandler
    : IRequestHandler<GetDailyAttendanceQuery, IReadOnlyList<AttendanceRecordDto>>
{
    private readonly IAttendanceRecordRepository _repository;

    public GetDailyAttendanceQueryHandler(IAttendanceRecordRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> Handle(
        GetDailyAttendanceQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetByDateAsync(request.WorkDate, cancellationToken);
        return items.Select(r => new AttendanceRecordDto(
            r.Id,
            r.EmployeeInformationId,
            r.EmployeeInformation.FullName,
            r.WorkDate,
            r.ShiftTemplateId,
            r.ShiftTemplate?.Name,
            r.CheckInTime,
            r.CheckOutTime,
            r.DayHours,
            r.NightHours,
            r.Status,
            r.Notes,
            r.OverrideReason,
            r.CreatedAt,
            r.UpdatedAt)).ToList();
    }
}
