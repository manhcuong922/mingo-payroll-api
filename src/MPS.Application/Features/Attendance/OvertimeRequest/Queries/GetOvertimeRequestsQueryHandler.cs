using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public class GetOvertimeRequestsQueryHandler : IRequestHandler<GetOvertimeRequestsQuery, PagedResult<OvertimeRequestDto>>
{
    private readonly IOvertimeRequestRepository _repository;

    public GetOvertimeRequestsQueryHandler(IOvertimeRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<OvertimeRequestDto>> Handle(GetOvertimeRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(
            request.EmployeeInformationId, request.Status, request.FromDate, request.ToDate, page, size, cancellationToken);

        var dtos = items.Select(r => new OvertimeRequestDto(
            r.Id, r.EmployeeInformationId, r.EmployeeInformation?.FullName ?? string.Empty,
            r.WorkDate, r.OtStartTime, r.OtEndTime, r.OtHours, r.Status,
            r.Reason, r.ReviewNote, r.ReviewedByUserId, r.ReviewedAt, r.CreatedAt)).ToList();

        return new PagedResult<OvertimeRequestDto>(dtos, total, page, size);
    }
}
