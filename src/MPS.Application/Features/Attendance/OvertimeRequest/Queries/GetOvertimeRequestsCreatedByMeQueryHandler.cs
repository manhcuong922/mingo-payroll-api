using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public class GetOvertimeRequestsCreatedByMeQueryHandler
    : IRequestHandler<GetOvertimeRequestsCreatedByMeQuery, PagedResult<OvertimeRequestDto>>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetOvertimeRequestsCreatedByMeQueryHandler(
        IOvertimeRequestRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<OvertimeRequestDto>> Handle(
        GetOvertimeRequestsCreatedByMeQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        // v3: endpoint /created-by-me giờ mang nghĩa "PM inbox" — trả OT của NV
        // trong đơn PM là ResponsibleUserId, KHÔNG phải OT do PM tạo (v1). Tên endpoint
        // giữ để BE-FE contract không vỡ.
        var (items, total) = await _repository.GetPagedAsync(
            employeeInformationId: null,
            status: request.Status,
            fromDate: request.FromDate,
            toDate: request.ToDate,
            createdByUserId: null,
            projectId: request.ProjectId,
            projectResponsibleUserId: userId,
            page: page,
            size: size,
            cancellationToken: cancellationToken);

        var dtos = items.Select(OvertimeRequestMapper.ToDto).ToList();
        return new PagedResult<OvertimeRequestDto>(dtos, total, page, size);
    }
}
