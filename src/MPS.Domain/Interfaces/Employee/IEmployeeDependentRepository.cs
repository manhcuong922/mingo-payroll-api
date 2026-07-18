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
}
