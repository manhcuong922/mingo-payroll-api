using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetPendingOvertimeAttendanceQueryHandler
    : IRequestHandler<GetPendingOvertimeAttendanceQuery, IReadOnlyList<PendingOvertimeAttendanceDto>>
{
    private readonly IOvertimeRequestRepository _repository;

    public GetPendingOvertimeAttendanceQueryHandler(IOvertimeRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PendingOvertimeAttendanceDto>> Handle(
        GetPendingOvertimeAttendanceQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetApprovedWithoutAttendanceOnDateAsync(
            request.WorkDate, cancellationToken);

        return items.Select(ot => new PendingOvertimeAttendanceDto(
            ot.Id,
            ot.EmployeeInformationId,
            ot.EmployeeInformation?.FullName ?? string.Empty,
            ot.WorkDate,
            ot.OtStartTime,
            ot.OtEndTime,
            ot.OtHours,
            ot.ProjectId,
            ot.Project?.Title)).ToList();
    }
}
