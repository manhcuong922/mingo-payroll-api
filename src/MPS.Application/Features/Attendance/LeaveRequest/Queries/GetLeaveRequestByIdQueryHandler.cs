using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public class GetLeaveRequestByIdQueryHandler : IRequestHandler<GetLeaveRequestByIdQuery, LeaveRequestDto?>
{
    private readonly ILeaveRequestRepository _repository;

    public GetLeaveRequestByIdQueryHandler(ILeaveRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<LeaveRequestDto?> Handle(GetLeaveRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (r is null) return null;
        return new LeaveRequestDto(
            r.Id, r.EmployeeInformationId, r.EmployeeInformation?.FullName ?? string.Empty,
            r.LeaveTypeId, r.LeaveType?.Code ?? string.Empty, r.LeaveType?.Name ?? string.Empty,
            r.StartDate, r.EndDate, r.TotalDays, r.IsPaid, r.Status,
            r.Reason, r.SupportingDocumentUrl, r.ReviewNote, r.ReviewedByUserId, r.ReviewedAt, r.CreatedAt);
    }
}
