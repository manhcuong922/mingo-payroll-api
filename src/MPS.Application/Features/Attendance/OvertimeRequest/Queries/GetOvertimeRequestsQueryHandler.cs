using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

/// <summary>
/// List OT cho HR/DM/admin — auto-filter theo permission user để tránh HR thấy đơn
/// PM forward cho DM và ngược lại:
///   - Có ONLY Approve.Hr → filter ApproverRoute=Hr.
///   - Có ONLY Approve.Department → filter ApproverRoute=DepartmentManager
///     AND Project.DepartmentId = DM's DepartmentId (BR-11).
///   - Có cả 2 permission (admin) → không filter.
///   - Không có approve nào nhưng có Read.All (observer role) → không filter, thấy hết.
/// </summary>
public class GetOvertimeRequestsQueryHandler : IRequestHandler<GetOvertimeRequestsQuery, PagedResult<OvertimeRequestDto>>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetOvertimeRequestsQueryHandler(
        IOvertimeRequestRepository repository,
        IEmployeeInformationRepository employeeRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<OvertimeRequestDto>> Handle(GetOvertimeRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        // MyScope=true (từ trang approval inbox) → tự động lọc route + dept theo permission
        // của user. MyScope=false (mặc định, dùng cho bảng chấm công, calendar…) → không lọc,
        // trả về mọi đơn user có quyền Read.All.
        var (routeFilter, deptFilter) = request.MyScope
            ? await ResolveScopeFiltersAsync(cancellationToken)
            : (null, null);

        var (items, total) = await _repository.GetPagedAsync(
            request.EmployeeInformationId, request.Status, request.FromDate, request.ToDate,
            request.CreatedByUserId, request.ProjectId, projectResponsibleUserId: null,
            page: page, size: size,
            approverRoute: routeFilter,
            projectDepartmentId: deptFilter,
            cancellationToken: cancellationToken);

        var dtos = items.Select(OvertimeRequestMapper.ToDto).ToList();

        return new PagedResult<OvertimeRequestDto>(dtos, total, page, size);
    }

    /// <summary>
    /// Tính route + dept filter theo permission user hiện tại.
    /// Return (null, null) nghĩa là không lọc — thấy hết.
    /// </summary>
    private async Task<(OvertimeApproverRoute? Route, int? DepartmentId)> ResolveScopeFiltersAsync(
        CancellationToken cancellationToken)
    {
        var canApproveHr = _currentUser.HasPermission(Permissions.OvertimeRequestApproveHr);
        var canApproveDm = _currentUser.HasPermission(Permissions.OvertimeRequestApproveDepartment);

        // Có cả 2 (admin) hoặc không có approve nào (observer) → không lọc.
        if (canApproveHr == canApproveDm) return (null, null);

        if (canApproveHr) return (OvertimeApproverRoute.Hr, null);

        // canApproveDm only — cần dept của user.
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");
        var dm = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee {employeeId} not found.");
        var deptId = dm.DepartmentId
            ?? throw new DomainValidationException("DM chưa gắn phòng ban — không xác định được inbox.");

        return (OvertimeApproverRoute.DepartmentManager, deptId);
    }
}
