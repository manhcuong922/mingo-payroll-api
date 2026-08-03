using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetAttendanceByRangeQueryHandler
    : IRequestHandler<GetAttendanceByRangeQuery, IReadOnlyList<AttendanceRecordDto>>
{
    private const int MaxRangeDays = 366;

    private readonly IAttendanceRecordRepository _repository;
    private readonly IDayTypeResolver _dayTypeResolver;

    public GetAttendanceByRangeQueryHandler(
        IAttendanceRecordRepository repository,
        IDayTypeResolver dayTypeResolver)
    {
        _repository = repository;
        _dayTypeResolver = dayTypeResolver;
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

        // BR-23: gom OnLeave theo NV → gọi ResolveRangeAsync 1 lần/NV để phân biệt paid/unpaid,
        // tránh N+1 khi range dài. NV nào không có OnLeave thì bỏ qua hẳn.
        var onLeaveEmpIds = items.Where(r => r.Status == AttendanceStatus.OnLeave)
            .Select(r => r.EmployeeInformationId).Distinct().ToList();
        var paidLeaveDates = new HashSet<(int EmpId, DateOnly Date)>();
        foreach (var empId in onLeaveEmpIds)
        {
            var map = await _dayTypeResolver.ResolveRangeAsync(
                empId, request.FromDate, request.ToDate, cancellationToken);
            foreach (var (date, dayType) in map)
                if (dayType == DayType.PaidLeaveDay)
                    paidLeaveDates.Add((empId, date));
        }

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
            OvertimeProjectTitle: r.OvertimeRequest?.Project?.Title,
            IsPaidLeave: r.Status == AttendanceStatus.OnLeave
                && paidLeaveDates.Contains((r.EmployeeInformationId, r.WorkDate)))).ToList();
    }
}
