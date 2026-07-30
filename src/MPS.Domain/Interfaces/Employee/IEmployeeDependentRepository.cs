using MPS.Domain.Entities.Employee;

namespace MPS.Domain.Interfaces.Employee;

public interface IEmployeeDependentRepository
{
    Task<IReadOnlyList<EmployeeDependent>> GetByEmployeeAsync(
        int employeeInformationId,
        bool onlyActive,
        CancellationToken cancellationToken = default);

    Task<EmployeeDependent?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CountActiveByEmployeeAsync(
        int employeeInformationId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveWithCitizenIdAsync(
        int employeeInformationId,
        string citizenId,
        int? excludeDependentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Người phụ thuộc đang hiệu lực mang CCCD này — dò trên TOÀN CÔNG TY, không giới hạn trong một
    /// nhân viên: mỗi người phụ thuộc chỉ được một người lao động tính giảm trừ gia cảnh.
    /// Trả về kèm nhân viên đang đăng ký để báo lỗi nêu rõ vướng ở đâu.
    /// </summary>
    Task<EmployeeDependent?> FindActiveByCitizenIdAsync(
        string citizenId,
        int? excludeDependentId,
        CancellationToken cancellationToken = default);
}
