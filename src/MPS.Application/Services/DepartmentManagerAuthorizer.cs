using MPS.Domain.Interfaces;

namespace MPS.Application.Services;

public sealed class DepartmentManagerAuthorizer : IDepartmentManagerAuthorizer
{
    private readonly IDepartmentRepository _departments;

    public DepartmentManagerAuthorizer(IDepartmentRepository departments)
    {
        _departments = departments;
    }

    // Mô hình mới: "quản lý xưởng" = nhân viên có role ProjectManager (đã gác ở permission attribute)
    // TRỰC THUỘC xưởng đó. Nhiều PM/xưởng đều hợp lệ; không còn ràng buộc Department.ManagerId 1-1.
    public async Task<bool> IsManagerOfAsync(int userId, int departmentId, CancellationToken ct)
    {
        var myDept = await _departments.GetDepartmentIdOfUserAsync(userId, ct);
        return myDept == departmentId;
    }

    public Task<int?> GetManagedDepartmentIdAsync(int userId, CancellationToken ct) =>
        _departments.GetDepartmentIdOfUserAsync(userId, ct);
}
