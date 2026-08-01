using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>Xem <see cref="IAttendanceApproverResolver"/>.</summary>
public sealed class AttendanceApproverResolver : IAttendanceApproverResolver
{
    private readonly INotificationRecipientDirectory _directory;
    private readonly IEmployeeInformationRepository _employees;

    public AttendanceApproverResolver(
        INotificationRecipientDirectory directory,
        IEmployeeInformationRepository employees)
    {
        _directory = directory;
        _employees = employees;
    }

    public async Task<IReadOnlyList<int>> ResolveForSubmitAsync(
        AttendanceRequestKind kind,
        OvertimeApproverRoute? approverRoute,
        int employeeInformationId,
        int? tierOneApproverUserId,
        int? excludeUserId,
        CancellationToken ct)
    {
        var ids = new HashSet<int>();

        switch (kind)
        {
            case AttendanceRequestKind.Overtime:
                if (tierOneApproverUserId is int pm) ids.Add(pm);
                break;

            case AttendanceRequestKind.Leave:
            case AttendanceRequestKind.LateEarly:
            case AttendanceRequestKind.Wfh:
                foreach (var id in await ResolveByRouteAsync(approverRoute, employeeInformationId, ct))
                    ids.Add(id);
                break;

            case AttendanceRequestKind.Adjustment:
                foreach (var id in await _directory.GetUserIdsByPermissionsAsync(
                    new[] { Permissions.AttendanceAdjustmentApproveAll }, ct))
                    ids.Add(id);
                break;

            case AttendanceRequestKind.Dependent:
                foreach (var id in await _directory.GetUserIdsByPermissionsAsync(
                    new[] { Permissions.DependentRequestApproveAll }, ct))
                    ids.Add(id);
                break;
        }

        if (excludeUserId is int me) ids.Remove(me);
        return ids.ToArray();
    }

    private async Task<IReadOnlyCollection<int>> ResolveByRouteAsync(
        OvertimeApproverRoute? route, int employeeInformationId, CancellationToken ct)
    {
        if (route is null) return Array.Empty<int>();

        return route switch
        {
            OvertimeApproverRoute.Hr =>
                await _directory.GetUserIdsByRolesAsync(new[] { SystemRoles.HrId }, ct),

            OvertimeApproverRoute.Director =>
                await _directory.GetUserIdsByRolesAsync(new[] { SystemRoles.DirectorId }, ct),

            OvertimeApproverRoute.DepartmentManager =>
                await ResolveDmSameDeptAsync(employeeInformationId, ct),

            _ => Array.Empty<int>(),
        };
    }

    /// <summary>
    /// DM đủ tư cách = có role DepartmentManager VÀ thuộc phòng ban của NV tạo đơn (đối xứng với
    /// <c>ApproverDepartmentGuard.EnsureCanApproveAsync</c>). Tính bằng giao 2 tập user id.
    /// </summary>
    private async Task<IReadOnlyCollection<int>> ResolveDmSameDeptAsync(int employeeInformationId, CancellationToken ct)
    {
        var ei = await _employees.GetByIdAsync(employeeInformationId, ct);
        if (ei?.DepartmentId is not int deptId) return Array.Empty<int>();

        var dmUsers = await _directory.GetUserIdsByRolesAsync(new[] { SystemRoles.DepartmentManagerId }, ct);
        if (dmUsers.Count == 0) return Array.Empty<int>();

        var deptMembers = await _directory.GetUserIdsByDepartmentsAsync(new[] { deptId }, ct);
        if (deptMembers.Count == 0) return Array.Empty<int>();

        // Giao 2 tập — HashSet giúp lookup O(1).
        var deptSet = new HashSet<int>(deptMembers);
        return dmUsers.Where(deptSet.Contains).ToArray();
    }
}
