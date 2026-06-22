using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public class GetMyLeaveRequestsQueryHandler : IRequestHandler<GetMyLeaveRequestsQuery, PagedResult<LeaveRequestDto>>
{
    private readonly ILeaveRequestRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyLeaveRequestsQueryHandler(
        ILeaveRequestRepository repository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<LeaveRequestDto>> Handle(GetMyLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(
            employeeId, request.Status, null, null, page, size, cancellationToken);

        var dtos = items.Select(r => new LeaveRequestDto(
            r.Id, r.EmployeeInformationId, r.EmployeeInformation?.FullName ?? string.Empty,
            r.LeaveTypeId, r.LeaveType?.Code ?? string.Empty, r.LeaveType?.Name ?? string.Empty,
            r.StartDate, r.EndDate, r.TotalDays, r.IsPaid, r.Status,
            r.Reason, r.SupportingDocumentUrl, r.ReviewNote, r.ReviewedByUserId, r.ReviewedAt, r.CreatedAt)).ToList();

        return new PagedResult<LeaveRequestDto>(dtos, total, page, size);
    }
}
