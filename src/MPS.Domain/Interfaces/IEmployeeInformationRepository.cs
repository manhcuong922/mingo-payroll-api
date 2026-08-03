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

    /// <summary>
    /// Tài khoản còn hoạt động (<c>User.Status == 1</c>). Khác <see cref="HasActiveUserAsync"/> ở
    /// chỗ tra theo <c>userId</c> — dùng cho nhánh TẠO hồ sơ, khi chưa có EmployeeInformation.Id.
    /// </summary>
    Task<bool> IsUserActiveAsync(int userId, CancellationToken cancellationToken = default);

    // RoleId của một User (null nếu user không tồn tại). Dùng để chặn gán role quản trị cấp công ty vào xưởng.
    Task<int?> GetUserRoleIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// #1 — CCCD có unique index ở DB nhưng trước đây không được kiểm ở tầng ứng dụng ⇒ trùng CCCD
    /// trả 500 thay vì 400 kèm thông báo. Đối xứng với <see cref="EmailExistsAsync"/>.
    /// </summary>
    Task<bool> CitizenIdExistsAsync(string citizenId, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mã số thuế đã thuộc về NV khác chưa. Mỗi cá nhân chỉ có MỘT MST dùng suốt đời
    /// (Điều 30 k.3 Luật Quản lý thuế 2019) — hai người khai chung một mã thì tờ khai thuế TNCN
    /// và quyết toán của cả hai đều sai.
    /// </summary>
    Task<bool> TaxNoExistsAsync(string taxNo, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Số sổ BHXH đã thuộc về NV khác chưa. Mỗi người lao động chỉ được cấp MỘT sổ với một số
    /// duy nhất (Điều 96 Luật BHXH 2014) — trùng số làm quá trình đóng của hai người dồn vào
    /// cùng một sổ.
    /// </summary>
    Task<bool> SocialNoExistsAsync(string socialNo, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<string?> GetPositionCodeAsync(int positionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đổi RIÊNG xưởng của một hồ sơ (kể cả gỡ về <c>null</c>), không đụng field nào khác.
    ///
    /// <para>Cần thiết vì endpoint sửa hồ sơ dùng ngữ nghĩa MERGE — ở đó <c>DepartmentId = null</c>
    /// nghĩa là "không gửi, giữ nguyên", nên không có cách nào diễn đạt "gỡ khỏi xưởng".</para>
    ///
    /// <para>Trả <c>false</c> nếu không tìm thấy hồ sơ.</para>
    /// </summary>
    Task<bool> SetDepartmentAsync(int id, int? departmentId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(EmployeeInformation entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(EmployeeInformation entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
