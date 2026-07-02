namespace MPS.Application.Services;

/// <summary>
/// Kiểm tra quyền tổ trưởng xưởng — dùng chung cho Task module.
/// Gom logic bắc cầu User → EmployeeInformation → Department vào một chỗ để tránh lặp.
/// Lưu ý: permission là điều kiện CẦN, service này là điều kiện ĐỦ (xem Lưu ý kỹ thuật #7).
/// </summary>
public interface IDepartmentManagerAuthorizer
{
    /// <summary>true nếu <paramref name="userId"/> là tổ trưởng (Department.ManagerId) của <paramref name="departmentId"/>.</summary>
    Task<bool> IsManagerOfAsync(int userId, int departmentId, CancellationToken ct);

    /// <summary>Xưởng mà <paramref name="userId"/> đang làm tổ trưởng (null nếu không quản lý xưởng nào).</summary>
    Task<int?> GetManagedDepartmentIdAsync(int userId, CancellationToken ct);
}
