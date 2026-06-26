using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetAttendanceSummaryQueryHandler
    : IRequestHandler<GetAttendanceSummaryQuery, PagedResult<EmployeeAttendanceSummaryDto>>
{
    private readonly IAttendanceRecordRepository _repository;

    public GetAttendanceSummaryQueryHandler(IAttendanceRecordRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<EmployeeAttendanceSummaryDto>> Handle(
        GetAttendanceSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        // Pull all matching records — handler aggregates into per-employee summaries
        var (items, _) = await _repository.GetSummaryPagedAsync(
            request.FromDate, request.ToDate, request.EmployeeInformationId, 1, int.MaxValue, cancellationToken);

        var grouped = items.GroupBy(r => new { r.EmployeeInformationId, r.EmployeeInformation.FullName })
            .Select(g => new EmployeeAttendanceSummaryDto(
                g.Key.EmployeeInformationId,
                g.Key.FullName,
                g.Count(r => r.Status == AttendanceStatus.Present),
                g.Count(r => r.Status == AttendanceStatus.Absent),
                g.Count(r => r.Status == AttendanceStatus.Late),
                g.Count(r => r.Status == AttendanceStatus.HalfDay),
                g.Count(r => r.Status == AttendanceStatus.OnLeave),
                g.Count(r => r.Status == AttendanceStatus.EarlyLeave),
                g.Count()))
            .OrderBy(d => d.EmployeeFullName)
            .ToList();

        var total = grouped.Count;
        var paged = grouped.Skip((page - 1) * size).Take(size).ToList();
        return new PagedResult<EmployeeAttendanceSummaryDto>(paged, total, page, size);
    }
}
