using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Queries;

public sealed class GetAttendanceLockStatusQueryHandler
    : IRequestHandler<GetAttendanceLockStatusQuery, AttendanceLockStatusDto>
{
    private readonly IAttendanceEditLockChecker _checker;

    public GetAttendanceLockStatusQueryHandler(IAttendanceEditLockChecker checker)
    {
        _checker = checker;
    }

    public Task<AttendanceLockStatusDto> Handle(
        GetAttendanceLockStatusQuery request,
        CancellationToken cancellationToken) =>
        _checker.GetStatusAsync(request.WorkDate, cancellationToken);
}
