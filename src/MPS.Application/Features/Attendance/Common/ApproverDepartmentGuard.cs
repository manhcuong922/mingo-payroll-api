using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Gác duyệt đơn (Leave, LateEarly, Wfh) — HR duyệt cross-department; DepartmentManager
/// (display: "Planning Manager") chỉ duyệt được đơn của nhân viên cùng phòng ban.
///
/// Dùng song song với <c>EnsureApproverRouteMatches</c> — Route match check role,
/// guard này check phạm vi phòng ban khi role = DepartmentManager.
/// </summary>
public sealed class ApproverDepartmentGuard
{
    private readonly IDepartmentManagerAuthorizer _dmAuthorizer;
    private readonly IEmployeeInformationRepository _employeeRepository;

    public ApproverDepartmentGuard(
        IDepartmentManagerAuthorizer dmAuthorizer,
        IEmployeeInformationRepository employeeRepository)
    {
        _dmAuthorizer = dmAuthorizer;
        _employeeRepository = employeeRepository;
    }

    /// <summary>
    /// HR/Admin/DeputyDirector → cho qua (cross-department). DepartmentManager →
    /// verify cùng phòng ban với nhân viên tạo đơn. Role khác → 403.
    /// </summary>
    public async Task EnsureCanApproveAsync(
        int approverUserId,
        int? approverRoleId,
        int requesterEmployeeInformationId,
        CancellationToken cancellationToken)
    {
        if (approverRoleId is null)
            throw new ForbiddenAccessException("Không xác định được vai trò của người duyệt.");

        // HR / SystemAdmin / DeputyDirector duyệt cross-department.
        if (approverRoleId == SystemRoles.HrId
            || approverRoleId == SystemRoles.SystemAdminId
            || approverRoleId == SystemRoles.DeputyDirectorId
            || approverRoleId == SystemRoles.DirectorId)
        {
            return;
        }

        if (approverRoleId != SystemRoles.DepartmentManagerId)
            throw new ForbiddenAccessException("Chỉ HR hoặc Quản lý phòng ban được duyệt đơn.");

        var requester = await _employeeRepository.GetByIdAsync(requesterEmployeeInformationId, cancellationToken)
            ?? throw new NotFoundException($"Employee {requesterEmployeeInformationId} not found.");

        if (requester.DepartmentId is not int requesterDepartmentId)
            throw new DomainValidationException(
                "Nhân viên tạo đơn chưa gán phòng ban — không xác định được quản lý phòng ban duyệt.");

        var isSameDepartment = await _dmAuthorizer.IsManagerOfAsync(
            approverUserId, requesterDepartmentId, cancellationToken);

        if (!isSameDepartment)
            throw new ForbiddenAccessException(
                "Bạn không phải quản lý phòng ban của đơn này — không được duyệt.");
    }
}
