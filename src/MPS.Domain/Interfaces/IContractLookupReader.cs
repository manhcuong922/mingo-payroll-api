namespace MPS.Domain.Interfaces;

/// <summary>
/// Read-only typed lookups required by Contract handlers (existence + projection checks).
/// Lives in Domain alongside the other repository interfaces; the EF-based implementation
/// is in Infrastructure. Keeping it here lets Application/Domain stay EF-free.
/// </summary>
public interface IContractLookupReader
{
    Task<UserSnapshot?> GetUserSnapshotAsync(int userId, CancellationToken cancellationToken);
    Task<EmployeeInfoSnapshot?> GetEmployeeInfoSnapshotAsync(int employeeInformationId, CancellationToken cancellationToken);
    Task<int?> GetEmployeeInformationIdByUserIdAsync(int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Khoảng phục vụ của 1 NV suy từ các HĐ không bị Cancelled. Dùng cho BR-15 (thâm niên + số
    /// tháng làm việc khi cấp quỹ phép năm). Null nếu NV chưa có HĐ hợp lệ nào.
    /// </summary>
    Task<EmployeeServiceWindow?> GetEmployeeServiceWindowAsync(int employeeInformationId, CancellationToken cancellationToken);

    /// <summary>
    /// Khoảng phục vụ của MỌI NV có ít nhất một HĐ không bị Cancelled — nguồn cho job cấp quỹ phép
    /// đầu năm. NV chưa có HĐ nào không xuất hiện trong danh sách (job bỏ qua, không ném lỗi).
    /// </summary>
    Task<IReadOnlyList<EmployeeServiceWindow>> GetAllEmployeeServiceWindowsAsync(CancellationToken cancellationToken);
    Task<bool> IsActiveUserAsync(int userId, CancellationToken cancellationToken);
    Task<bool> IsActiveContractPartyAsync(int partyId, CancellationToken cancellationToken);
    Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken);
    Task<SalaryGradeSnapshot?> GetSalaryGradeSnapshotAsync(int salaryGradeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetActiveAllowanceTypeIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    Task<bool> IsActiveAllowanceTypeAsync(int allowanceTypeId, CancellationToken cancellationToken);

    Task<TerminationReasonRefSnapshot?> GetTerminationReasonRefAsync(int reasonRefId, CancellationToken cancellationToken);
}

/// <summary>
/// Khoảng phục vụ của NV theo hợp đồng. <paramref name="JoinDate"/> = MIN(EffectiveFrom),
/// <paramref name="ServiceEnd"/> = MAX(EffectiveTo) — <c>null</c> khi có ít nhất một HĐ không xác
/// định thời hạn (phủ vô hạn về sau).
/// </summary>
public sealed record EmployeeServiceWindow(
    int EmployeeInformationId,
    DateTime JoinDate,
    DateTime? ServiceEnd,
    int? DepartmentId);

public sealed record UserSnapshot(int Id, int Status, DateTime Dob, string FullName);

public sealed record EmployeeInfoSnapshot(int Id, DateTime Dob, string FullName);

public sealed record SalaryGradeSnapshot(int Id, int PositionId, decimal BaseSalary, string Currency, bool IsActive);

public sealed record TerminationReasonRefSnapshot(int Id, string Code, bool RequiresWrittenNotice);
