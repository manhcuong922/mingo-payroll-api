using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Queries;

public class GetMyAttendanceAdjustmentsQueryHandler
    : IRequestHandler<GetMyAttendanceAdjustmentsQuery, PagedResult<AttendanceAdjustmentDto>>
{
    private readonly IAttendanceAdjustmentRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyAttendanceAdjustmentsQueryHandler(
        IAttendanceAdjustmentRepository repository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AttendanceAdjustmentDto>> Handle(
        GetMyAttendanceAdjustmentsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(
            employeeId, request.Status, page, size, cancellationToken);

        var dtos = items.Select(a => new AttendanceAdjustmentDto(
            a.Id,
            a.AttendanceRecordId,
            a.EmployeeInformationId,
            a.EmployeeInformation?.FullName ?? string.Empty,
            a.AttendanceRecord.WorkDate,
            a.NewStatus,
            a.NewCheckInAt,
            a.NewCheckOutAt,
            a.OriginalStatus,
            a.OriginalCheckInAt,
            a.OriginalCheckOutAt,
            a.Reason,
            a.Status,
            a.ReviewNote,
            a.ReviewedByUserId,
            a.ReviewedAt,
            a.CreatedAt)).ToList();

        return new PagedResult<AttendanceAdjustmentDto>(dtos, total, page, size);
    }
}
