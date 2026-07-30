using MPS.Domain.Entities.Employee;
using MPS.Domain.Enums.Employee;

namespace MPS.Domain.Interfaces.Employee;

public interface IDependentRegistrationRequestRepository
{
    Task<(IReadOnlyList<DependentRegistrationRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        DependentRequestStatus? status,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<DependentRegistrationRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(DependentRegistrationRequest entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(DependentRegistrationRequest entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve trong transaction: dựa vào RequestType, tạo/cập nhật/deactivate EmployeeDependent,
    /// đồng thời sync EmployeeInformation.DependentsNo bằng count active dependents.
    /// </summary>
    Task<bool> ApproveAndApplyAsync(
        int requestId,
        int reviewedByUserId,
        string? reviewNote,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
