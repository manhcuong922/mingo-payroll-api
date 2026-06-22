using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public class GetOvertimeRequestByIdQueryHandler : IRequestHandler<GetOvertimeRequestByIdQuery, OvertimeRequestDto?>
{
    private readonly IOvertimeRequestRepository _repository;

    public GetOvertimeRequestByIdQueryHandler(IOvertimeRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<OvertimeRequestDto?> Handle(GetOvertimeRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (r is null) return null;
        return new OvertimeRequestDto(
            r.Id, r.EmployeeInformationId, r.EmployeeInformation?.FullName ?? string.Empty,
            r.WorkDate, r.OtStartTime, r.OtEndTime, r.OtHours, r.Status,
            r.Reason, r.ReviewNote, r.ReviewedByUserId, r.ReviewedAt, r.CreatedAt);
    }
}
