namespace MPS.Application.Features.Notifications.Recipients;

/// <summary>
/// Danh bạ tra cứu user id cho module thông báo (chỉ ĐỌC). Cầu nối tới dữ liệu nhân sự
/// (Users / EmployeeInformation) mà KHÔNG gắn module notification vào AppDbContext —
/// impl nằm ở Infrastructure, notification chỉ phụ thuộc interface này.
///
/// Thêm cách chọn người nhận mới trong tương lai (vd theo Position, theo quản lý trực tiếp)
/// = thêm 1 method ở đây + impl, KHÔNG sửa RecipientSpec/Resolver lõi.
/// </summary>
public interface INotificationRecipientDirectory
{
    /// <summary>User id đang hoạt động thuộc các role.</summary>
    Task<IReadOnlyCollection<int>> GetUserIdsByRolesAsync(
        IReadOnlyCollection<int> roleIds, CancellationToken ct = default);

    /// <summary>User id đang hoạt động thuộc các phòng ban/xưởng (qua EmployeeInformation).</summary>
    Task<IReadOnlyCollection<int>> GetUserIdsByDepartmentsAsync(
        IReadOnlyCollection<int> departmentIds, CancellationToken ct = default);

    /// <summary>Toàn bộ user id đang hoạt động (broadcast).</summary>
    Task<IReadOnlyCollection<int>> GetAllActiveUserIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// User id đang hoạt động có (bất kỳ) permission code nào trong tập. Union — 1 code khớp là đủ.
    /// Dùng cho recipient theo permission (Phase 1 mở kênh email + fan-out nghiệp vụ).
    /// </summary>
    Task<IReadOnlyCollection<int>> GetUserIdsByPermissionsAsync(
        IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default);
}
