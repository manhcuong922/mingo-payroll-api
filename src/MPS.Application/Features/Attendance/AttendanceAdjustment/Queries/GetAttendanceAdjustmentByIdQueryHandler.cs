using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Queries;

public class GetAttendanceAdjustmentByIdQueryHandler
    : IRequestHandler<GetAttendanceAdjustmentByIdQuery, AttendanceAdjustmentDto?>
{
    private readonly IAttendanceAdjustmentRepository _repository;

    public GetAttendanceAdjustmentByIdQueryHandler(IAttendanceAdjustmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<AttendanceAdjustmentDto?> Handle(
        GetAttendanceAdjustmentByIdQuery request, CancellationToken cancellationToken)
    {
        var a = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (a is null) return null;
        return new AttendanceAdjustmentDto(
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
            a.CreatedAt);
    }
}
