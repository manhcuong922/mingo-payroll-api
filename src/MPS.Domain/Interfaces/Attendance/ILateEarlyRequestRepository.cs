using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface ILateEarlyRequestRepository
{
    Task<(IReadOnlyList<LateEarlyRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        LateEarlyRequestStatus? status,
        LateEarlyKind? kind,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        int? departmentIdFilter = null,
        OvertimeApproverRoute? approverRouteFilter = null,
        CancellationToken cancellationToken = default);

    Task<LateEarlyRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> AddAsync(LateEarlyRequest entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(LateEarlyRequest entity, CancellationToken cancellationToken = default);

    Task<int> CountApprovedInMonthAsync(
        int employeeInformationId,
        int year,
        int month,
        LateEarlyKind kind,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlapAsync(
        int employeeInformationId,
        DateOnly workDate,
        LateEarlyKind kind,
        int? excludeRequestId,
        CancellationToken cancellationToken = default);
}
