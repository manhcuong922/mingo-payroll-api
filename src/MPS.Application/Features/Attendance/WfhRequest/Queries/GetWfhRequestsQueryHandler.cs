using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.WfhRequest.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Queries;

public class GetWfhRequestsQueryHandler : IRequestHandler<GetWfhRequestsQuery, PagedResult<WfhRequestDto>>
{
    private readonly IWfhRequestRepository _repository;

    public GetWfhRequestsQueryHandler(IWfhRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<WfhRequestDto>> Handle(GetWfhRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);
        var (items, total) = await _repository.GetPagedAsync(
            request.EmployeeInformationId, request.Status, request.FromDate, request.ToDate, page, size, cancellationToken);
        var dtos = items.Select(WfhRequestMapper.ToDto).ToList();
        return new PagedResult<WfhRequestDto>(dtos, total, page, size);
    }
}
