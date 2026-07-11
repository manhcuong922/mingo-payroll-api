using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.WfhRequest.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Queries;

public class GetMyWfhRequestsQueryHandler : IRequestHandler<GetMyWfhRequestsQuery, PagedResult<WfhRequestDto>>
{
    private readonly IWfhRequestRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyWfhRequestsQueryHandler(
        IWfhRequestRepository repository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<WfhRequestDto>> Handle(GetMyWfhRequestsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);
        var (items, total) = await _repository.GetPagedAsync(
            employeeId, request.Status, null, null, page, size, cancellationToken);
        var dtos = items.Select(WfhRequestMapper.ToDto).ToList();
        return new PagedResult<WfhRequestDto>(dtos, total, page, size);
    }
}
