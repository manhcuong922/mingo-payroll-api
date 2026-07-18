using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Queries;

public class GetAttendanceAdjustmentsQueryHandler
    : IRequestHandler<GetAttendanceAdjustmentsQuery, PagedResult<AttendanceAdjustmentDto>>
{
    private readonly IAttendanceAdjustmentRepository _repository;

    public GetAttendanceAdjustmentsQueryHandler(IAttendanceAdjustmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<AttendanceAdjustmentDto>> Handle(
        GetAttendanceAdjustmentsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(
            request.EmployeeInformationId, request.Status, page, size, cancellationToken);

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
