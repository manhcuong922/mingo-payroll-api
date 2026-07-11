using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.LeaveRequest.Common;
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

        var dtos = items.Select(LeaveRequestMapper.ToDto).ToList();
        return new PagedResult<LeaveRequestDto>(dtos, total, page, size);
    }
}
