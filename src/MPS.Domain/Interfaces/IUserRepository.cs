using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch lookup cho import flow (vd parse excel bảng phân ca). Trả về map
    /// EmployeeCode → EmployeeInformationId cho các user có liên kết EmployeeInformation.
    /// User chưa link EmployeeInformation bị bỏ qua, caller tự coi như "không tìm thấy".
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetEmployeeInformationIdsByCodesAsync(
        IEnumerable<string> employeeCodes,
        CancellationToken cancellationToken = default);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// User còn hoạt động (<c>Status == 1</c>) thuộc các role chỉ định, sắp theo Id.
    /// Dùng cho job auto-sinh bảng đánh giá tháng (Loại B).
    /// </summary>
    Task<IReadOnlyList<User>> GetActiveByRoleIdsAsync(
        IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        int? roleId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> EmployeeCodeExistsAsync(string employeeCode, int? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> RoleExistsAsync(int roleId, CancellationToken cancellationToken = default);
    Task<int> CountByEmployeeCodePrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Task<int> AddAsync(User entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo User mới + gán EI.UserId = newUser.Id trong 1 transaction.
    /// Caller đã validate EI tồn tại, UserId IS NULL, và có Active contract.
    /// </summary>
    Task<int> AddWithEmployeeInfoLinkAsync(User user, int employeeInformationId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(User entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateLastLoginAsync(int userId, DateTime loginAt, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set <c>User.Status = 0</c> cho các User đang Active (<c>Status = 1</c>) đã gắn với
    /// <see cref="EmployeeInformation"/> nằm trong <paramref name="employeeInformationIds"/>
    /// VÀ EI đó không còn HĐ nào <see cref="MPS.Domain.Enums.Contract.ContractStatus.Active"/>.
    /// Idempotent: nếu User đã inactive hoặc EI vẫn còn HĐ Active khác thì bỏ qua.
    /// Dùng bởi <c>ContractExpiryJob</c> khi HĐ hết hạn.
    /// </summary>
    /// <returns>Số User đã bị chuyển sang inactive.</returns>
    Task<int> DeactivateForEmployeesWithoutActiveContractAsync(
        IReadOnlyCollection<int> employeeInformationIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
