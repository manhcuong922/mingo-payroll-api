using MPS.Domain.Interfaces;

namespace MPS.Application.Services;

public sealed class DepartmentManagerAuthorizer : IDepartmentManagerAuthorizer
{
    private readonly IDepartmentRepository _departments;

    public DepartmentManagerAuthorizer(IDepartmentRepository departments)
    {
        _departments = departments;
    }

    public async Task<bool> IsManagerOfAsync(int userId, int departmentId, CancellationToken ct)
    {
        var managed = await _departments.GetByManagerUserIdAsync(userId, ct);
        return managed is not null && managed.Id == departmentId;
    }

    public async Task<int?> GetManagedDepartmentIdAsync(int userId, CancellationToken ct)
    {
        var managed = await _departments.GetByManagerUserIdAsync(userId, ct);
        return managed?.Id;
    }
}
