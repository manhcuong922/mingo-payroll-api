namespace MPS.Application.Services;

/// <summary>
/// Kiểm tra quyền quản lý xưởng — dùng chung cho Task module.
/// Mô hình: "quản lý xưởng" = nhân viên có role ProjectManager TRỰC THUỘC xưởng đó
/// (qua EmployeeInformation.DepartmentId). Nhiều PM/xưởng đều hợp lệ.
/// Lưu ý: permission (Task*.Department) là điều kiện CẦN — gác ở attribute; service này
/// kiểm điều kiện ĐỦ về phạm vi xưởng (user thuộc đúng xưởng của task/đơn).
/// </summary>
public interface IDepartmentManagerAuthorizer
{
    /// <summary>true nếu <paramref name="userId"/> trực thuộc <paramref name="departmentId"/> (xưởng của mình).</summary>
    Task<bool> IsManagerOfAsync(int userId, int departmentId, CancellationToken ct);

    /// <summary>Xưởng mà <paramref name="userId"/> trực thuộc (null nếu chưa gán xưởng / chưa có hồ sơ NV).</summary>
    Task<int?> GetManagedDepartmentIdAsync(int userId, CancellationToken ct);
}
