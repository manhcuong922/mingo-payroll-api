using Microsoft.EntityFrameworkCore;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Notifications.Recipients;

/// <summary>
/// Impl danh bạ người nhận — ĐỌC dữ liệu nhân sự từ AppDbContext (read-only, KHÔNG ghi/sửa).
/// Đây là điểm DUY NHẤT module thông báo chạm tới dữ liệu module khác, và chỉ để tra user id.
/// </summary>
public sealed class NotificationRecipientDirectory : INotificationRecipientDirectory
{
    // User.Status = 1 nghĩa là đang hoạt động (xem EmployeeAccountProvisioner khi tạo tài khoản).
    private const int ActiveStatus = 1;

    private readonly AppDbContext _db;

    public NotificationRecipientDirectory(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<int>> GetUserIdsByRolesAsync(
        IReadOnlyCollection<int> roleIds, CancellationToken ct = default)
    {
        if (roleIds.Count == 0) return Array.Empty<int>();

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Status == ActiveStatus && roleIds.Contains(u.RoleId))
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<int>> GetUserIdsByDepartmentsAsync(
        IReadOnlyCollection<int> departmentIds, CancellationToken ct = default)
    {
        if (departmentIds.Count == 0) return Array.Empty<int>();

        // Người thuộc phòng ban xác định qua EmployeeInformation.DepartmentId → UserId.
        return await _db.EmployeeInformations
            .AsNoTracking()
            .Where(e => e.UserId != null
                && e.DepartmentId != null
                && departmentIds.Contains(e.DepartmentId.Value)
                && e.User!.Status == ActiveStatus)
            .Select(e => e.UserId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<int>> GetAllActiveUserIdsAsync(CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Status == ActiveStatus)
            .Select(u => u.Id)
            .ToListAsync(ct);
    }
}
