using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Queries;

public class GetLateEarlyRequestsQueryHandler : IRequestHandler<GetLateEarlyRequestsQuery, PagedResult<LateEarlyRequestDto>>
{
    private readonly ILateEarlyRequestRepository _repository;

    public GetLateEarlyRequestsQueryHandler(ILateEarlyRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<LateEarlyRequestDto>> Handle(GetLateEarlyRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);
        var (items, total) = await _repository.GetPagedAsync(
            request.EmployeeInformationId, request.Status, request.Kind,
            request.FromDate, request.ToDate, page, size, cancellationToken);
        var dtos = items.Select(LateEarlyRequestMapper.ToDto).ToList();
        return new PagedResult<LateEarlyRequestDto>(dtos, total, page, size);
    }
}
