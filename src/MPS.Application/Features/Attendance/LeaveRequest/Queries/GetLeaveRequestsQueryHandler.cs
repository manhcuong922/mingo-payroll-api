using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public class GetLeaveRequestsQueryHandler : IRequestHandler<GetLeaveRequestsQuery, PagedResult<LeaveRequestDto>>
{
    private readonly ILeaveRequestRepository _repository;

    public GetLeaveRequestsQueryHandler(ILeaveRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<LeaveRequestDto>> Handle(GetLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(
            request.EmployeeInformationId, request.Status, request.FromDate, request.ToDate, page, size, cancellationToken);

        var dtos = items.Select(r => new LeaveRequestDto(
            r.Id, r.EmployeeInformationId, r.EmployeeInformation?.FullName ?? string.Empty,
            r.LeaveTypeId, r.LeaveType?.Code ?? string.Empty, r.LeaveType?.Name ?? string.Empty,
            r.StartDate, r.EndDate, r.TotalDays, r.IsPaid, r.Status,
            r.Reason, r.SupportingDocumentUrl, r.ReviewNote, r.ReviewedByUserId, r.ReviewedAt, r.CreatedAt)).ToList();

        return new PagedResult<LeaveRequestDto>(dtos, total, page, size);
    }
}
