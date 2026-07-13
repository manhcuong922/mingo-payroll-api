using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Queries;

public class GetLateEarlyRequestsQueryHandler : IRequestHandler<GetLateEarlyRequestsQuery, PagedResult<LateEarlyRequestDto>>
{
    private readonly ILateEarlyRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDepartmentManagerAuthorizer _dmAuthorizer;

    public GetLateEarlyRequestsQueryHandler(
        ILateEarlyRequestRepository repository,
        ICurrentUserService currentUser,
        IDepartmentManagerAuthorizer dmAuthorizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _dmAuthorizer = dmAuthorizer;
    }

    public async Task<PagedResult<LateEarlyRequestDto>> Handle(GetLateEarlyRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        // DM (Planning Manager) chỉ thấy đơn của phòng ban mình. HR/Admin thấy tất.
        // Director chỉ thấy đơn HR đẩy lên (route=Director).
        int? departmentIdFilter = null;
        OvertimeApproverRoute? approverRouteFilter = null;
        if (_currentUser.RoleId == SystemRoles.DepartmentManagerId && _currentUser.UserId is int uid)
        {
            var deptId = await _dmAuthorizer.GetManagedDepartmentIdAsync(uid, cancellationToken);
            if (deptId is null)
                return new PagedResult<LateEarlyRequestDto>(new List<LateEarlyRequestDto>(), 0, page, size);
            departmentIdFilter = deptId;
        }
        else if (_currentUser.RoleId == SystemRoles.DirectorId)
        {
            approverRouteFilter = OvertimeApproverRoute.Director;
        }

        var (items, total) = await _repository.GetPagedAsync(
            request.EmployeeInformationId, request.Status, request.Kind,
            request.FromDate, request.ToDate, page, size, departmentIdFilter, approverRouteFilter, cancellationToken);
        var dtos = items.Select(LateEarlyRequestMapper.ToDto).ToList();
        return new PagedResult<LateEarlyRequestDto>(dtos, total, page, size);
    }
}
