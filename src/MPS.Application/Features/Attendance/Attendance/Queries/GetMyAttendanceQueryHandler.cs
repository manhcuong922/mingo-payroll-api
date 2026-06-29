using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
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

    public GetMyAttendanceQueryHandler(
        IAttendanceRecordRepository repository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _lookup = lookup;
        _currentUser = currentUser;
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

        return items.Select(r => new AttendanceRecordDto(
            r.Id,
            r.EmployeeInformationId,
            r.EmployeeInformation?.FullName ?? string.Empty,
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
