using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

// Projection cho danh sách chọn tổ trưởng — tránh trả nguyên EmployeeInformation (thừa PII).
public sealed record EligibleManager(
    int Id, string FullName, string? PositionName, int RoleId, string RoleName);

public interface IDepartmentRepository
{
    // GetAll / GetPaged mặc định lọc IsActive == true; truyền includeInactive=true để xem cả xưởng đã archive.
    Task<IReadOnlyList<Department>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task<Department?> GetByIdAsync(int id, CancellationToken ct);
    Task<Department?> GetByManagerEmployeeIdAsync(int employeeId, CancellationToken ct);
    Task<(IReadOnlyList<Department> Items, int Total)> GetPagedAsync(
        string? keyword, bool includeInactive, int page, int pageSize, CancellationToken ct);

    Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct);
    Task<bool> HasEmployeesAsync(int id, CancellationToken ct);

    // Manager (EmployeeInformation) phải tồn tại và có UserId (đăng nhập được) — xem Lưu ý #9.
    Task<bool> ManagerExistsWithUserAsync(int managerEmployeeId, CancellationToken ct);

    // Danh sách nhân viên đủ điều kiện làm tổ trưởng: có tài khoản (UserId != null)
    // và User.RoleId nằm trong roleIds (whitelist). keyword lọc theo FullName (chứa, không phân biệt hoa thường).
    Task<IReadOnlyList<EligibleManager>> GetEligibleManagersAsync(
        string? keyword, IReadOnlyList<int> roleIds, CancellationToken ct);

    Task<int> AddAsync(Department entity, CancellationToken ct);
    Task<bool> UpdateAsync(Department entity, CancellationToken ct);

    // Soft-delete: set IsActive=false (KHÔNG hard-delete — xem Lưu ý #10).
    Task<bool> ArchiveAsync(int id, CancellationToken ct);

    // ===== Bắc cầu User ↔ Department qua EmployeeInformation (dùng cho Task module) =====

    // Xưởng mà 1 USER TRỰC THUỘC (EmployeeInformation.DepartmentId của user đó). null nếu chưa gán xưởng
    // hoặc user chưa có hồ sơ nhân viên. Đây là cơ sở cho scoped access mô hình mới:
    // "quản lý xưởng" = nhân viên có role ProjectManager (permission gác ở attribute) VÀ thuộc xưởng đó.
    Task<int?> GetDepartmentIdOfUserAsync(int userId, CancellationToken ct);

    // USER là công nhân của xưởng (EmployeeInformation.DepartmentId, chỉ những bản ghi có UserId != null).
    Task<IReadOnlyList<int>> GetMemberUserIdsAsync(int departmentId, CancellationToken ct);
}
