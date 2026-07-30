using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetAttendanceByRangeQueryHandler
    : IRequestHandler<GetAttendanceByRangeQuery, IReadOnlyList<AttendanceRecordDto>>
{
    private const int MaxRangeDays = 366;

    private readonly IAttendanceRecordRepository _repository;

    public GetAttendanceByRangeQueryHandler(IAttendanceRecordRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> Handle(
        GetAttendanceByRangeQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ToDate < request.FromDate)
            throw new DomainValidationException("ToDate phải ≥ FromDate.");

        var span = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;
        if (span > MaxRangeDays)
            throw new DomainValidationException($"Khoảng ngày tối đa {MaxRangeDays} ngày, hiện {span}.");

        var items = await _repository.GetByRangeAsync(request.FromDate, request.ToDate, cancellationToken);
        return items.Select(r => new AttendanceRecordDto(
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
            OvertimeProjectTitle: r.OvertimeRequest?.Project?.Title)).ToList();
    }
}
