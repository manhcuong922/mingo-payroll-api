using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IEmployeeInformationRepository
{
    Task<EmployeeInformation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EmployeeInformation?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeInformation>> GetAllAsync(
        bool? hasActiveUser = null,
        string? keyword = null,
        CancellationToken cancellationToken = default);

    /// <summary>Employee có gắn User và User.Status == 1. Dùng cho attendance/phân ca thay cho contract-active.</summary>
    Task<bool> HasActiveUserAsync(int employeeInformationId, CancellationToken cancellationToken = default);

    /// <summary>Lọc trong danh sách id, trả về những id mà EmployeeInformation có User.Status == 1.</summary>
    Task<IReadOnlyList<int>> GetEmployeeInformationIdsWithActiveUserAsync(
        IEnumerable<int> employeeInformationIds,
        CancellationToken cancellationToken = default);

    // Scoped access: danh sách nhân viên thuộc 1 phòng ban (dùng cho EmployeeInformation.Read.Department).
    Task<IReadOnlyList<EmployeeInformation>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<EmployeeInformation> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        int? positionId,
        bool? hasAccount,
        bool? hasActiveUser,
        int page,
        int pageSize,
        int? departmentId = null,
        CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default);

    // RoleId của một User (null nếu user không tồn tại). Dùng để chặn gán role quản trị cấp công ty vào xưởng.
    Task<int?> GetUserRoleIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<string?> GetPositionCodeAsync(int positionId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(EmployeeInformation entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(EmployeeInformation entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
